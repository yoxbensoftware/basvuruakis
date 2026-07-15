using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BasvuruAkis.Api;
using BasvuruAkis.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BasvuruAkis.Api.Tests;

public sealed class ApplicationFlowTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"basvuruakis-{Guid.NewGuid():N}.db");
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Database:Provider"] = "Sqlite",
                        ["ConnectionStrings:Sqlite"] = $"Data Source={_dbPath}",
                        ["Security:EncryptionKey"] = "test-encryption-key",
                        ["Security:LookupKey"] = "test-lookup-key",
                        ["Otp:MaxRequestsPerIpPerHour"] = "100",
                        ["Otp:MaxRequestsPerDevicePerHour"] = "2"
                    });
                });
            });
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
                // Test correctness must not depend on immediate SQLite file handle release on Windows.
            }
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PublicApplicationFlow_CreatesAssignedApplication_AndAdminCanReadMaskedListAndDetail()
    {
        var otpRequest = await _client.PostAsJsonAsync("/api/otp/request", new OtpRequestDto("+90 532 111 22 33", "ok", "test-device"));
        otpRequest.EnsureSuccessStatusCode();
        var otp = await ReadJson<OtpRequestResponse>(otpRequest);
        Assert.False(string.IsNullOrWhiteSpace(otp.DevelopmentCode));

        var otpVerify = await _client.PostAsJsonAsync("/api/otp/verify", new OtpVerifyDto("+90 532 111 22 33", otp.DevelopmentCode!, "test-device"));
        otpVerify.EnsureSuccessStatusCode();
        var verification = await ReadJson<OtpVerifyResponse>(otpVerify);
        Assert.False(string.IsNullOrWhiteSpace(verification.VerificationToken));

        var applicationRequest = new CreateApplicationRequest(
            "Ayşe",
            "Yılmaz",
            "10000000146",
            "+90 532 111 22 33",
            "ayse.yilmaz@example.test",
            34,
            3401,
            340101,
            "Caferağa Mahallesi Test Sokak No:1",
            "34710",
            true,
            true,
            verification.VerificationToken,
            $"idem-{Guid.NewGuid():N}");

        var applicationResponse = await _client.PostAsJsonAsync("/api/applications", applicationRequest);
        Assert.Equal(HttpStatusCode.Created, applicationResponse.StatusCode);
        var created = await ReadJson<ApplicationCreatedResponse>(applicationResponse);
        Assert.Equal("Assigned", created.Status);

        var loginResponse = await _client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest("admin@basvuruakis.local", "ChangeMe!12345", null));
        loginResponse.EnsureSuccessStatusCode();
        var login = await ReadJson<LoginResponse>(loginResponse);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var listResponse = await _client.GetAsync("/api/admin/applications?page=1&pageSize=10");
        listResponse.EnsureSuccessStatusCode();
        var list = await ReadJson<PagedResult<ApplicationListItem>>(listResponse);
        Assert.True(list.Total >= 1);
        Assert.Contains(list.Items, x => x.Id == created.Id && x.NationalIdMasked.StartsWith("hash:", StringComparison.Ordinal));

        var detailResponse = await _client.GetAsync($"/api/admin/applications/{created.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await ReadJson<ApplicationDetailResponse>(detailResponse);
        Assert.Equal("10000000146", detail.NationalId);
        Assert.Equal("+905321112233".Replace("+", "", StringComparison.Ordinal), detail.Phone);
    }

    [Fact]
    public async Task OtpRequest_RateLimitsByDevice_AndWritesSecurityLog()
    {
        _client.DefaultRequestHeaders.Remove("CF-Connecting-IP");
        _client.DefaultRequestHeaders.Add("CF-Connecting-IP", "203.0.113.10");

        var first = await _client.PostAsJsonAsync("/api/otp/request", new OtpRequestDto("05321112240", "ok", "same-device"));
        var second = await _client.PostAsJsonAsync("/api/otp/request", new OtpRequestDto("05321112241", "ok", "same-device"));
        var third = await _client.PostAsJsonAsync("/api/otp/request", new OtpRequestDto("05321112242", "ok", "same-device"));

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, third.StatusCode);
        var error = await ReadJson<ApiError>(third);
        Assert.Equal("otp_device_rate_limited", error.Code);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var securityLogs = await db.SecurityLogs.AsNoTracking().ToListAsync();
        Assert.Contains(securityLogs, x => x.EventType == "otp.rate_limit.device" && x.IpAddress == "203.0.113.10");
    }

    [Fact]
    public async Task ApplicationEndpoint_RejectsDuplicateNationalIdOrPhone_WithGenericMessage()
    {
        var first = await CreateVerifiedApplicationAsync("05321112234", "ayse1@example.test", $"idem-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await CreateVerifiedApplicationAsync("05321112235", "ayse2@example.test", $"idem-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var error = await ReadJson<ApiError>(second);
        Assert.Equal("duplicate_application", error.Code);
    }

    private async Task<HttpResponseMessage> CreateVerifiedApplicationAsync(string phone, string email, string idempotencyKey)
    {
        var otpRequest = await _client.PostAsJsonAsync("/api/otp/request", new OtpRequestDto(phone, "ok", "test-device"));
        otpRequest.EnsureSuccessStatusCode();
        var otp = await ReadJson<OtpRequestResponse>(otpRequest);

        var otpVerify = await _client.PostAsJsonAsync("/api/otp/verify", new OtpVerifyDto(phone, otp.DevelopmentCode!, "test-device"));
        otpVerify.EnsureSuccessStatusCode();
        var verification = await ReadJson<OtpVerifyResponse>(otpVerify);

        return await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Ayşe",
            "Yılmaz",
            "10000000146",
            phone,
            email,
            34,
            3401,
            340101,
            "Caferağa Mahallesi Test Sokak No:1",
            null,
            true,
            true,
            verification.VerificationToken,
            idempotencyKey));
    }

    private static async Task<T> ReadJson<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(value);
        return value;
    }
}
