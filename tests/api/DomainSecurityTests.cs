using System.Net;
using System.Text.Json;
using BasvuruAkis.Api;
using BasvuruAkis.Api.Data;
using BasvuruAkis.Api.Domain;
using BasvuruAkis.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BasvuruAkis.Api.Tests;

public sealed class DomainSecurityTests
{
    [Theory]
    [InlineData("10000000146", true)]
    [InlineData("10000000145", false)]
    [InlineData("01234567890", false)]
    [InlineData("abc", false)]
    public void TcknValidator_ValidatesAlgorithmicRules(string value, bool expected)
    {
        var validator = new TcknValidator();

        var actual = validator.IsValid(value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CryptoService_EncryptsAndHashesSensitiveValues()
    {
        var provider = new StaticKeyProvider();
        var crypto = new CryptoService(provider);

        var encrypted = crypto.Encrypt("10000000146");
        var decrypted = crypto.Decrypt(encrypted);
        var firstHash = crypto.HashLookup("10000000146");
        var secondHash = crypto.HashLookup("10000000146");

        Assert.NotEqual("10000000146", encrypted);
        Assert.Equal("10000000146", decrypted);
        Assert.Equal(firstHash, secondHash);
        Assert.Equal(64, firstHash.Length);
    }

    [Theory]
    [InlineData("=cmd|'/C calc'!A0")]
    [InlineData("+SUM(1,1)")]
    [InlineData("-10+20")]
    [InlineData("@malicious")]
    [InlineData(" \t=HYPERLINK(\"http://example.test\")")]
    public void ExportSanitizer_PrefixesFormulaInjectionPayloads(string payload)
    {
        var sanitizer = new ExportSanitizer();

        var actual = sanitizer.SanitizeCell(payload);

        Assert.StartsWith("'", actual);
    }

    [Fact]
    public void PostgresConnectionStrings_NormalizesRenderDatabaseUrl()
    {
        var normalized = PostgresConnectionStrings.Normalize(
            "postgresql://user%2Dname:p%40ss%3Aword@dpg-test-a:5432/basvuruakis");

        var builder = new NpgsqlConnectionStringBuilder(normalized);
        Assert.Equal("dpg-test-a", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("basvuruakis", builder.Database);
        Assert.Equal("user-name", builder.Username);
        Assert.Equal("p@ss:word", builder.Password);
    }

    [Fact]
    public void PostgresConnectionStrings_LeavesNpgsqlConnectionStringUnchanged()
    {
        const string connectionString = "Host=localhost;Port=5432;Database=basvuruakis;Username=test;Password=secret";

        var normalized = PostgresConnectionStrings.Normalize(connectionString);

        Assert.Equal(connectionString, normalized);
    }

    [Fact]
    public async Task ProductionBootstrap_CreatesFirstAdminWithMfaAndSuperAdminPermissions()
    {
        await using var fixture = await ProductionBootstrapFixture.CreateAsync(new Dictionary<string, string?>
        {
            ["AdminBootstrap:Email"] = "owner@example.test",
            ["AdminBootstrap:Password"] = "CorrectHorse!42X",
            ["AdminBootstrap:MfaSecret"] = "JBSWY3DPEHPK3PXP"
        });

        await ProductionBootstrap.EnsureAdminAsync(fixture.Services);

        var db = fixture.Services.GetRequiredService<AppDbContext>();
        var admin = await db.AdminUsers.Include(x => x.Permissions).SingleAsync();
        Assert.Equal("owner@example.test", admin.Email);
        Assert.True(admin.MfaEnabled);
        Assert.Equal("JBSWY3DPEHPK3PXP", admin.MfaSecret);
        Assert.True(BCrypt.Net.BCrypt.Verify("CorrectHorse!42X", admin.PasswordHash));
        Assert.Equal(Permissions.SuperAdmin.Order(), admin.Permissions.Select(x => x.Permission).Order());
    }

    [Fact]
    public async Task ProductionBootstrap_RejectsMfaDisabledProductionAdmins()
    {
        await using var fixture = await ProductionBootstrapFixture.CreateAsync(new Dictionary<string, string?>());
        var db = fixture.Services.GetRequiredService<AppDbContext>();
        db.AdminUsers.Add(new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectHorse!42X"),
            MfaEnabled = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ProductionBootstrap.EnsureAdminAsync(fixture.Services));
        Assert.Contains("MFA enabled", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProductionBootstrap_RejectsInvalidProductionAdminMfaSecrets()
    {
        await using var fixture = await ProductionBootstrapFixture.CreateAsync(new Dictionary<string, string?>());
        var db = fixture.Services.GetRequiredService<AppDbContext>();
        db.AdminUsers.Add(new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectHorse!42X"),
            MfaEnabled = true,
            MfaSecret = "not-valid",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ProductionBootstrap.EnsureAdminAsync(fixture.Services));
        Assert.Contains("MFA secret is invalid", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProductionBootstrap_RejectsInvalidProductionIntegrationConfiguration()
    {
        await using var fixture = await ProductionBootstrapFixture.CreateAsync(new Dictionary<string, string?>
        {
            ["Sms:Provider"] = "log-only"
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ProductionBootstrap.EnsureAdminAsync(fixture.Services));
        Assert.Contains("Sms:Provider", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DemoSeed_RefreshesPublicCopyForExistingPresentationData()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"basvuruakis-seed-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

        await using var provider = services.BuildServiceProvider();
        try
        {
            var db = provider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.ContentPages.Add(new ContentPage
            {
                Id = Guid.NewGuid(),
                Slug = "iletisim",
                Title = "İletişim",
                Summary = "Eski özet",
                Body = "Demo ortamında iletişim bilgileri yönetim panelinden güncellenir.",
                SeoTitle = "İletişim",
                SeoDescription = "Eski açıklama",
                Status = PublishStatus.Published,
                PublishedAt = DateTimeOffset.UtcNow
            });
            db.LegalTexts.AddRange(
                new LegalText
                {
                    Id = Guid.NewGuid(),
                    Type = LegalTextType.PrivacyNotice,
                    Version = "2026-07-15",
                    Title = "KVKK Aydınlatma Metni",
                    Body = "Bu metin demo teknik şablondur. Hukuki içerik onaylanmalıdır.",
                    IsActive = true,
                    PublishedAt = DateTimeOffset.UtcNow
                },
                new LegalText
                {
                    Id = Guid.NewGuid(),
                    Type = LegalTextType.ExplicitConsent,
                    Version = "2026-07-15",
                    Title = "Açık Rıza Metni",
                    Body = "Bu açık rıza metni demo teknik şablondur.",
                    IsActive = true,
                    PublishedAt = DateTimeOffset.UtcNow
                });
            await db.SaveChangesAsync();

            await DemoSeed.SeedAsync(provider);

            var contact = await db.ContentPages.AsNoTracking().SingleAsync(x => x.Slug == "iletisim");
            var activeLegalTexts = await db.LegalTexts.AsNoTracking().Where(x => x.IsActive).ToListAsync();
            Assert.DoesNotContain("Demo", contact.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(activeLegalTexts, x => x.Body.Contains("demo", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(activeLegalTexts, x => x.Type == LegalTextType.CookiePolicy);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try
                {
                    File.Delete(dbPath);
                }
                catch (IOException)
                {
                    // Windows can hold SQLite files briefly after disposal.
                }
            }
        }
    }

    [Fact]
    public void TotpService_NormalizesBase32Secrets()
    {
        Assert.Equal("JBSWY3DPEHPK3PXP", TotpService.NormalizeSecret("jb swy3dp-ehpk3pxp"));

        Assert.Throws<InvalidOperationException>(() => TotpService.NormalizeSecret("not-valid"));
    }

    [Fact]
    public async Task SmsProvider_SendsProductionHttpJsonRequest()
    {
        var handler = new RecordingHandler(HttpStatusCode.Accepted);
        var provider = new SmsProvider(
            ProductionSmsConfiguration(new Dictionary<string, string?>()),
            new TestEnvironment(),
            new StaticHttpClientFactory(handler),
            LoggerFactory.Create(_ => { }).CreateLogger<SmsProvider>());

        await provider.SendOtpAsync("905321112233", "123456", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal("https://sms.example.test/messages", handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-sms-api-key", handler.AuthorizationParameter);
        Assert.NotNull(handler.JsonPayload);
        var root = handler.JsonPayload.RootElement;
        Assert.Equal("905321112233", root.GetProperty("to").GetString());
        Assert.Equal("BasvuruAkis", root.GetProperty("sender").GetString());
        Assert.Contains("123456", root.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SmsProvider_RejectsUnsupportedProductionProviders()
    {
        var handler = new RecordingHandler(HttpStatusCode.Accepted);
        var provider = new SmsProvider(
            ProductionSmsConfiguration(new Dictionary<string, string?>
            {
                ["Sms:Provider"] = "log-only"
            }),
            new TestEnvironment(),
            new StaticHttpClientFactory(handler),
            LoggerFactory.Create(_ => { }).CreateLogger<SmsProvider>());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SendOtpAsync("905321112233", "123456", CancellationToken.None));
        Assert.Contains("Unsupported production SMS provider", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, handler.SendCount);
    }

    private static IConfiguration ProductionSmsConfiguration(Dictionary<string, string?> overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["Sms:Provider"] = "http-json",
            ["Sms:ApiKey"] = "test-sms-api-key",
            ["Sms:Endpoint"] = "https://sms.example.test/messages",
            ["Sms:Sender"] = "BasvuruAkis",
            ["Sms:MessageTemplate"] = "Kodunuz: {code}"
        };
        foreach (var (key, value) in overrides)
        {
            values[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed class StaticKeyProvider : IDataProtectionKeyProvider
    {
        public byte[] EncryptionKey { get; } = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
        public byte[] LookupKey { get; } = Enumerable.Range(32, 32).Select(x => (byte)x).ToArray();
    }

    private sealed class StaticHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int SendCount { get; private set; }
        public HttpMethod? RequestMethod { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public JsonDocument JsonPayload { get; private set; } = null!;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount += 1;
            RequestMethod = request.Method;
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            var payload = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
            JsonPayload = JsonDocument.Parse(payload);
            return new HttpResponseMessage(statusCode);
        }
    }

    private sealed class ProductionBootstrapFixture : IAsyncDisposable
    {
        private readonly string _dbPath;

        private ProductionBootstrapFixture(ServiceProvider services, string dbPath)
        {
            Services = services;
            _dbPath = dbPath;
        }

        public ServiceProvider Services { get; }

        public static async Task<ProductionBootstrapFixture> CreateAsync(Dictionary<string, string?> configuration)
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"basvuruakis-bootstrap-{Guid.NewGuid():N}.db");
            var values = new Dictionary<string, string?>
            {
                ["AdminBootstrap:Email"] = "owner@example.test",
                ["AdminBootstrap:Password"] = "CorrectHorse!42X",
                ["AdminBootstrap:MfaSecret"] = "JBSWY3DPEHPK3PXP",
                ["Captcha:TurnstileSecret"] = "test-turnstile-secret",
                ["Sms:Provider"] = "http-json",
                ["Sms:ApiKey"] = "test-sms-api-key",
                ["Sms:Endpoint"] = "https://sms.example.test/messages",
                ["Sms:MessageTemplate"] = "Kodunuz: {code}"
            };
            foreach (var (key, value) in configuration)
            {
                values[key] = value;
            }

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
            services.AddSingleton<IWebHostEnvironment>(new TestEnvironment());
            services.AddSingleton<ISystemClock>(new TestClock());
            services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
            var provider = services.BuildServiceProvider();
            await provider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
            return new ProductionBootstrapFixture(provider, dbPath);
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            if (File.Exists(_dbPath))
            {
                try
                {
                    File.Delete(_dbPath);
                }
                catch (IOException)
                {
                    // Windows can hold SQLite files briefly after disposal.
                }
            }
        }
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "BasvuruAkis.Api.Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
