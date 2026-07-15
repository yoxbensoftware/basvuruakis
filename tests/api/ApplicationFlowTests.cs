using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BasvuruAkis.Api;
using BasvuruAkis.Api.Data;
using BasvuruAkis.Api.Domain;
using BasvuruAkis.Api.Services;
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
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IReferenceNumberGenerator, DeterministicReferenceNumberGenerator>();
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

        var auditResponse = await _client.GetAsync("/api/admin/audit-logs?page=1&pageSize=10&action=application.viewed");
        auditResponse.EnsureSuccessStatusCode();
        var auditLogs = await ReadJson<PagedResult<AuditLogItem>>(auditResponse);
        Assert.Contains(auditLogs.Items, x => x.EntityId == created.Id.ToString());
    }

    [Fact]
    public async Task AdminAuth_RefreshRotatesTokenAndRejectsOldToken()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest("admin@basvuruakis.local", "ChangeMe!12345", null));
        loginResponse.EnsureSuccessStatusCode();
        var login = await ReadJson<LoginResponse>(loginResponse);

        var refreshResponse = await _client.PostAsJsonAsync("/api/admin/auth/refresh", new RefreshTokenRequest(login.RefreshToken));
        refreshResponse.EnsureSuccessStatusCode();
        var refreshed = await ReadJson<LoginResponse>(refreshResponse);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);

        var reuseOldToken = await _client.PostAsJsonAsync("/api/admin/auth/refresh", new RefreshTokenRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseOldToken.StatusCode);

        var refreshNewToken = await _client.PostAsJsonAsync("/api/admin/auth/refresh", new RefreshTokenRequest(refreshed.RefreshToken));
        refreshNewToken.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AdminAuth_LogoutRevokesRefreshToken()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest("admin@basvuruakis.local", "ChangeMe!12345", null));
        loginResponse.EnsureSuccessStatusCode();
        var login = await ReadJson<LoginResponse>(loginResponse);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var logoutResponse = await _client.PostAsJsonAsync("/api/admin/auth/logout", new RefreshTokenRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshResponse = await _client.PostAsJsonAsync("/api/admin/auth/refresh", new RefreshTokenRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task AdminPermissions_RestrictDetailExportAndAuditEndpoints()
    {
        var applicationResponse = await CreateVerifiedApplicationAsync("05321112253", "limited-permission@example.test", $"idem-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Created, applicationResponse.StatusCode);
        var application = await ReadJson<ApplicationCreatedResponse>(applicationResponse);

        await AddAdminAsync("limited@basvuruakis.local", "Limited!12345", Permissions.ApplicationsRead);

        var loginResponse = await _client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest("limited@basvuruakis.local", "Limited!12345", null));
        loginResponse.EnsureSuccessStatusCode();
        var login = await ReadJson<LoginResponse>(loginResponse);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var listResponse = await _client.GetAsync("/api/admin/applications?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var detailResponse = await _client.GetAsync($"/api/admin/applications/{application.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, detailResponse.StatusCode);

        var exportResponse = await _client.PostAsJsonAsync("/api/admin/exports", new ExportRequest(ExportFormat.Csv, EmptyApplicationQuery()));
        Assert.Equal(HttpStatusCode.Forbidden, exportResponse.StatusCode);

        var auditResponse = await _client.GetAsync("/api/admin/audit-logs?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.Forbidden, auditResponse.StatusCode);

        var securityResponse = await _client.GetAsync("/api/admin/security-logs?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.Forbidden, securityResponse.StatusCode);
    }

    [Fact]
    public async Task AdminPermissions_RestrictDashboardAssignmentAndAnonymizeEndpoints()
    {
        var applicationResponse = await CreateVerifiedApplicationAsync("05321112254", "limited-ops@example.test", $"idem-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Created, applicationResponse.StatusCode);
        var application = await ReadJson<ApplicationCreatedResponse>(applicationResponse);

        await AddAdminAsync("limited-ops@basvuruakis.local", "Limited!12345", Permissions.ApplicationsRead);

        var loginResponse = await _client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest("limited-ops@basvuruakis.local", "Limited!12345", null));
        loginResponse.EnsureSuccessStatusCode();
        var login = await ReadJson<LoginResponse>(loginResponse);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var dashboardResponse = await _client.GetAsync("/api/admin/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, dashboardResponse.StatusCode);

        var assignmentResponse = await _client.PostAsJsonAsync($"/api/admin/applications/{application.Id}/assignment", new ManualAssignmentRequest(1, "Yetkisiz deneme"));
        Assert.Equal(HttpStatusCode.Forbidden, assignmentResponse.StatusCode);

        var anonymizeResponse = await _client.PostAsJsonAsync($"/api/admin/applications/{application.Id}/anonymize", new AnonymizeApplicationRequest("Yetkisiz deneme", true));
        Assert.Equal(HttpStatusCode.Forbidden, anonymizeResponse.StatusCode);
    }

    [Fact]
    public async Task ApplicationEndpoint_ReturnsSameApplicationForSameIdempotencyKey()
    {
        const string phone = "05321112252";
        var otpRequest = await _client.PostAsJsonAsync("/api/otp/request", new OtpRequestDto(phone, "ok", "test-device"));
        otpRequest.EnsureSuccessStatusCode();
        var otp = await ReadJson<OtpRequestResponse>(otpRequest);

        var otpVerify = await _client.PostAsJsonAsync("/api/otp/verify", new OtpVerifyDto(phone, otp.DevelopmentCode!, "test-device"));
        otpVerify.EnsureSuccessStatusCode();
        var verification = await ReadJson<OtpVerifyResponse>(otpVerify);
        var idempotencyKey = $"idem-{Guid.NewGuid():N}";
        var request = new CreateApplicationRequest(
            "Ayşe",
            "Yılmaz",
            "10000000146",
            phone,
            "idempotent@example.test",
            34,
            3401,
            340101,
            "Caferağa Mahallesi Test Sokak No:1",
            null,
            true,
            true,
            verification.VerificationToken,
            idempotencyKey);

        var firstResponse = await _client.PostAsJsonAsync("/api/applications", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = await ReadJson<ApplicationCreatedResponse>(firstResponse);

        var secondResponse = await _client.PostAsJsonAsync("/api/applications", request);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var second = await ReadJson<ApplicationCreatedResponse>(secondResponse);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.ReferenceNumber, second.ReferenceNumber);
        Assert.Equal(first.Status, second.Status);
    }

    [Fact]
    public async Task LegalText_AllowsOnlyOneActiveVersionPerType()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.LegalTexts.Add(new LegalText
        {
            Id = Guid.NewGuid(),
            Type = LegalTextType.PrivacyNotice,
            Version = "duplicate-active",
            Title = "Çakışan KVKK metni",
            Body = "Aynı türde ikinci aktif metin kabul edilmemeli.",
            IsActive = true,
            PublishedAt = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
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

        var loginResponse = await _client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest("admin@basvuruakis.local", "ChangeMe!12345", null));
        loginResponse.EnsureSuccessStatusCode();
        var login = await ReadJson<LoginResponse>(loginResponse);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var securityLogResponse = await _client.GetAsync("/api/admin/security-logs?page=1&pageSize=10&eventType=otp.rate_limit.device");
        securityLogResponse.EnsureSuccessStatusCode();
        var securityLogPage = await ReadJson<PagedResult<SecurityLogItem>>(securityLogResponse);
        Assert.Contains(securityLogPage.Items, x => x.IpAddress == "203.0.113.10");
    }

    [Fact]
    public async Task Cors_PreflightAllowsConfiguredOriginOnly()
    {
        var allowedResponse = await _client.SendAsync(CreatePreflightRequest("http://localhost:3000"));
        Assert.Equal(HttpStatusCode.NoContent, allowedResponse.StatusCode);
        Assert.Equal("http://localhost:3000", allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", allowedResponse.Headers.GetValues("Access-Control-Allow-Credentials").Single());

        var blockedResponse = await _client.SendAsync(CreatePreflightRequest("https://evil.example.test"));
        Assert.Equal(HttpStatusCode.NoContent, blockedResponse.StatusCode);
        Assert.False(blockedResponse.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(blockedResponse.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task AdminLogin_LocksAccountAfterMfaFailures()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.AdminUsers.SingleAsync(x => x.Email == "admin@basvuruakis.local");
            admin.MfaEnabled = true;
            admin.MfaSecret = "JBSWY3DPEHPK3PXP";
            await db.SaveChangesAsync();
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await _client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest("admin@basvuruakis.local", "ChangeMe!12345", "000000"));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await ReadJson<ApiError>(response);
            Assert.Equal("mfa_required", error.Code);
        }

        var locked = await _client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest("admin@basvuruakis.local", "ChangeMe!12345", "000000"));
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.AdminUsers.AsNoTracking().SingleAsync(x => x.Email == "admin@basvuruakis.local");
            Assert.Equal(5, admin.FailedLoginCount);
            Assert.True(admin.LockoutUntil > DateTimeOffset.UtcNow);

            var mfaFailures = await db.SecurityLogs.AsNoTracking()
                .CountAsync(x => x.EventType == "login.mfa_failed" && x.ActorUserId == admin.Id);
            Assert.Equal(5, mfaFailures);
        }
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

    [Fact]
    public async Task ApplicationEndpoint_RejectsInvalidLocationWithoutConsumingVerificationToken()
    {
        const string phone = "05321112250";
        var otpRequest = await _client.PostAsJsonAsync("/api/otp/request", new OtpRequestDto(phone, "ok", "test-device"));
        otpRequest.EnsureSuccessStatusCode();
        var otp = await ReadJson<OtpRequestResponse>(otpRequest);

        var otpVerify = await _client.PostAsJsonAsync("/api/otp/verify", new OtpVerifyDto(phone, otp.DevelopmentCode!, "test-device"));
        otpVerify.EnsureSuccessStatusCode();
        var verification = await ReadJson<OtpVerifyResponse>(otpVerify);

        var invalidLocation = new CreateApplicationRequest(
            "Ayşe",
            "Yılmaz",
            "10000000146",
            phone,
            "invalid-location@example.test",
            34,
            3401,
            999999,
            "Caferağa Mahallesi Test Sokak No:1",
            null,
            true,
            true,
            verification.VerificationToken,
            $"idem-{Guid.NewGuid():N}");

        var rejected = await _client.PostAsJsonAsync("/api/applications", invalidLocation);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var error = await ReadJson<ApiError>(rejected);
        Assert.Equal("location_not_found", error.Code);

        var validLocation = invalidLocation with
        {
            NeighborhoodId = 340101,
            IdempotencyKey = $"idem-{Guid.NewGuid():N}"
        };
        var accepted = await _client.PostAsJsonAsync("/api/applications", validLocation);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task ApplicationEndpoint_RetriesReferenceNumberCollision()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var crypto = scope.ServiceProvider.GetRequiredService<ICryptoService>();
            db.Applications.Add(new ApplicationRecord
            {
                Id = Guid.NewGuid(),
                ReferenceNumber = "BA-TEST-000001",
                FirstName = "Referans",
                LastName = "Çakışma",
                NationalIdEncrypted = crypto.Encrypt("99999999999"),
                NationalIdHash = crypto.HashLookup("99999999999"),
                PhoneEncrypted = crypto.Encrypt("905329990001"),
                PhoneHash = crypto.HashLookup("905329990001"),
                EmailEncrypted = crypto.Encrypt("reference-collision@example.test"),
                EmailHash = crypto.HashLookup("reference-collision@example.test"),
                AddressEncrypted = crypto.Encrypt("Test adresi"),
                ProvinceId = 34,
                DistrictId = 3401,
                NeighborhoodId = 340101,
                IsPhoneVerified = true,
                Status = ApplicationStatus.Submitted,
                IdempotencyKey = $"idem-{Guid.NewGuid():N}",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await CreateVerifiedApplicationAsync("05321112251", "reference-retry@example.test", $"idem-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadJson<ApplicationCreatedResponse>(response);
        Assert.Equal("BA-TEST-000002", created.ReferenceNumber);
    }

    [Fact]
    public async Task AnonymizeEndpoint_RemovesPersonalData_AndAllowsFreshApplication()
    {
        var firstResponse = await CreateVerifiedApplicationAsync("05321112260", "anon1@example.test", $"idem-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var created = await ReadJson<ApplicationCreatedResponse>(firstResponse);

        string oldPhoneHash;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            oldPhoneHash = await db.Applications
                .Where(x => x.Id == created.Id)
                .Select(x => x.PhoneHash)
                .SingleAsync();
        }

        var loginResponse = await _client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest("admin@basvuruakis.local", "ChangeMe!12345", null));
        loginResponse.EnsureSuccessStatusCode();
        var login = await ReadJson<LoginResponse>(loginResponse);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var missingConfirmationResponse = await _client.PostAsJsonAsync($"/api/admin/applications/{created.Id}/anonymize", new AnonymizeApplicationRequest("KVKK veri sahibi silme talebi", false));
        Assert.Equal(HttpStatusCode.BadRequest, missingConfirmationResponse.StatusCode);
        var missingConfirmation = await ReadJson<ApiError>(missingConfirmationResponse);
        Assert.Equal("confirmation_required", missingConfirmation.Code);

        var anonymizeResponse = await _client.PostAsJsonAsync($"/api/admin/applications/{created.Id}/anonymize", new AnonymizeApplicationRequest("KVKK veri sahibi silme talebi", true));
        anonymizeResponse.EnsureSuccessStatusCode();
        var anonymized = await ReadJson<ApplicationAnonymizedResponse>(anonymizeResponse);
        Assert.Equal("Anonymized", anonymized.Status);

        var detailResponse = await _client.GetAsync($"/api/admin/applications/{created.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await ReadJson<ApplicationDetailResponse>(detailResponse);
        Assert.Equal("Anonim", detail.FirstName);
        Assert.Equal("Kayıt", detail.LastName);
        Assert.Equal("", detail.NationalId);
        Assert.Equal("", detail.Phone);
        Assert.Equal("", detail.Email);
        Assert.Equal("", detail.Address);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var application = await db.Applications.AsNoTracking().SingleAsync(x => x.Id == created.Id);
            Assert.StartsWith("anon-national-", application.NationalIdHash, StringComparison.Ordinal);
            Assert.DoesNotContain(await db.OtpRequests.AsNoTracking().ToListAsync(), x => x.PhoneHash == oldPhoneHash);
            Assert.All(await db.ApplicationConsents.AsNoTracking().Where(x => x.ApplicationId == created.Id).ToListAsync(), consent =>
            {
                Assert.Equal("anonymized", consent.IpAddress);
                Assert.Equal("anonymized", consent.UserAgent);
            });
        }

        _client.DefaultRequestHeaders.Authorization = null;
        var secondResponse = await CreateVerifiedApplicationAsync("05321112260", "anon2@example.test", $"idem-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
    }

    [Fact]
    public async Task AdminApplicationFilters_SearchSensitiveFieldsCurrentOffice_AndExportUsesSameFilters()
    {
        var firstResponse = await CreateVerifiedApplicationAsync(
            "05321112270",
            "filtre1@example.test",
            $"idem-{Guid.NewGuid():N}",
            nationalId: "10000000214",
            firstName: "Elif",
            lastName: "Kaya");
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = await ReadJson<ApplicationCreatedResponse>(firstResponse);

        var secondResponse = await CreateVerifiedApplicationAsync(
            "05321112271",
            "filtre2@example.test",
            $"idem-{Guid.NewGuid():N}",
            nationalId: "10000000382",
            firstName: "Mert",
            lastName: "Demir");
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var second = await ReadJson<ApplicationCreatedResponse>(secondResponse);

        var loginResponse = await _client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest("admin@basvuruakis.local", "ChangeMe!12345", null));
        loginResponse.EnsureSuccessStatusCode();
        var login = await ReadJson<LoginResponse>(loginResponse);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var reassignment = await _client.PostAsJsonAsync($"/api/admin/applications/{second.Id}/assignment", new ManualAssignmentRequest(1, "Test temsilcilik filtresi"));
        Assert.Equal(HttpStatusCode.NoContent, reassignment.StatusCode);

        var byPhone = await ReadJson<PagedResult<ApplicationListItem>>(await _client.GetAsync("/api/admin/applications?page=1&pageSize=20&phone=05321112270"));
        Assert.Single(byPhone.Items);
        Assert.Equal(first.Id, byPhone.Items[0].Id);

        var byNationalId = await ReadJson<PagedResult<ApplicationListItem>>(await _client.GetAsync("/api/admin/applications?page=1&pageSize=20&nationalId=10000000382"));
        Assert.Single(byNationalId.Items);
        Assert.Equal(second.Id, byNationalId.Items[0].Id);

        var byEmail = await ReadJson<PagedResult<ApplicationListItem>>(await _client.GetAsync("/api/admin/applications?page=1&pageSize=20&email=filtre1@example.test"));
        Assert.Single(byEmail.Items);
        Assert.Equal(first.Id, byEmail.Items[0].Id);

        var byFirstName = await ReadJson<PagedResult<ApplicationListItem>>(await _client.GetAsync("/api/admin/applications?page=1&pageSize=20&firstName=eli"));
        Assert.Single(byFirstName.Items);
        Assert.Equal(first.Id, byFirstName.Items[0].Id);

        var byLastName = await ReadJson<PagedResult<ApplicationListItem>>(await _client.GetAsync("/api/admin/applications?page=1&pageSize=20&lastName=dem"));
        Assert.Single(byLastName.Items);
        Assert.Equal(second.Id, byLastName.Items[0].Id);

        var currentOfficeOne = await ReadJson<PagedResult<ApplicationListItem>>(await _client.GetAsync("/api/admin/applications?page=1&pageSize=20&representativeOfficeId=1"));
        Assert.Contains(currentOfficeOne.Items, x => x.Id == second.Id);
        Assert.DoesNotContain(currentOfficeOne.Items, x => x.Id == first.Id);

        var currentOfficeTwo = await ReadJson<PagedResult<ApplicationListItem>>(await _client.GetAsync("/api/admin/applications?page=1&pageSize=20&representativeOfficeId=2"));
        Assert.Contains(currentOfficeTwo.Items, x => x.Id == first.Id);
        Assert.DoesNotContain(currentOfficeTwo.Items, x => x.Id == second.Id);

        var assigned = await ReadJson<PagedResult<ApplicationListItem>>(await _client.GetAsync("/api/admin/applications?page=1&pageSize=20&isAssigned=true"));
        Assert.Contains(assigned.Items, x => x.Id == first.Id);
        Assert.Contains(assigned.Items, x => x.Id == second.Id);

        var unassigned = await ReadJson<PagedResult<ApplicationListItem>>(await _client.GetAsync("/api/admin/applications?page=1&pageSize=20&isAssigned=false"));
        Assert.Empty(unassigned.Items);

        var phoneVerified = await ReadJson<PagedResult<ApplicationListItem>>(await _client.GetAsync("/api/admin/applications?page=1&pageSize=20&isPhoneVerified=true"));
        Assert.Contains(phoneVerified.Items, x => x.Id == first.Id);
        Assert.Contains(phoneVerified.Items, x => x.Id == second.Id);

        var phoneNotVerified = await ReadJson<PagedResult<ApplicationListItem>>(await _client.GetAsync("/api/admin/applications?page=1&pageSize=20&isPhoneVerified=false"));
        Assert.Empty(phoneNotVerified.Items);

        var auditResponse = await _client.GetAsync("/api/admin/audit-logs?page=1&pageSize=10&action=application.assigned");
        auditResponse.EnsureSuccessStatusCode();
        var auditLogs = await ReadJson<PagedResult<AuditLogItem>>(auditResponse);
        Assert.Contains(auditLogs.Items, x =>
            x.EntityId == second.Id.ToString() &&
            x.MetadataJson.Contains("\"previousRepresentativeOfficeId\":2", StringComparison.Ordinal) &&
            x.MetadataJson.Contains("\"representativeOfficeId\":1", StringComparison.Ordinal));

        var exportResponse = await _client.PostAsJsonAsync("/api/admin/exports", new ExportRequest(
            ExportFormat.Csv,
            new ApplicationQuery(
                Page: null,
                PageSize: null,
                Sort: null,
                Desc: null,
                Status: null,
                FirstName: null,
                LastName: null,
                NationalId: null,
                Phone: "05321112270",
                Email: null,
                ProvinceId: null,
                DistrictId: null,
                NeighborhoodId: null,
                RepresentativeOfficeId: null,
                IsAssigned: null,
                IsPhoneVerified: null,
                From: null,
                To: null)));
        exportResponse.EnsureSuccessStatusCode();
        var csv = await exportResponse.Content.ReadAsStringAsync();
        Assert.Contains(first.ReferenceNumber, csv, StringComparison.Ordinal);
        Assert.DoesNotContain(second.ReferenceNumber, csv, StringComparison.Ordinal);
    }

    private async Task<HttpResponseMessage> CreateVerifiedApplicationAsync(
        string phone,
        string email,
        string idempotencyKey,
        string nationalId = "10000000146",
        string firstName = "Ayşe",
        string lastName = "Yılmaz")
    {
        var otpRequest = await _client.PostAsJsonAsync("/api/otp/request", new OtpRequestDto(phone, "ok", "test-device"));
        otpRequest.EnsureSuccessStatusCode();
        var otp = await ReadJson<OtpRequestResponse>(otpRequest);

        var otpVerify = await _client.PostAsJsonAsync("/api/otp/verify", new OtpVerifyDto(phone, otp.DevelopmentCode!, "test-device"));
        otpVerify.EnsureSuccessStatusCode();
        var verification = await ReadJson<OtpVerifyResponse>(otpVerify);

        return await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            firstName,
            lastName,
            nationalId,
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

    private async Task AddAdminAsync(string email, string password, params string[] permissions)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminId = Guid.NewGuid();
        db.AdminUsers.Add(new AdminUser
        {
            Id = adminId,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            MfaEnabled = false,
            CreatedAt = DateTimeOffset.UtcNow,
            Permissions = permissions
                .Select(permission => new AdminUserPermission { AdminUserId = adminId, Permission = permission })
                .ToList()
        });
        await db.SaveChangesAsync();
    }

    private static HttpRequestMessage CreatePreflightRequest(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/otp/request");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        return request;
    }

    private static async Task<T> ReadJson<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(value);
        return value;
    }

    private static ApplicationQuery EmptyApplicationQuery() => new(
        Page: null,
        PageSize: null,
        Sort: null,
        Desc: null,
        Status: null,
        FirstName: null,
        LastName: null,
        NationalId: null,
        Phone: null,
        Email: null,
        ProvinceId: null,
        DistrictId: null,
        NeighborhoodId: null,
        RepresentativeOfficeId: null,
        IsAssigned: null,
        IsPhoneVerified: null,
        From: null,
        To: null);

    private sealed class DeterministicReferenceNumberGenerator : IReferenceNumberGenerator
    {
        private int _next;

        public string Generate(DateTimeOffset now)
        {
            var suffix = Interlocked.Increment(ref _next).ToString("D6");
            return $"BA-TEST-{suffix}";
        }
    }
}
