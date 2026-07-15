using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BasvuruAkis.Api.Data;
using BasvuruAkis.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BasvuruAkis.Api.Services;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public static class Normalization
{
    public static string NormalizeDigits(string value) => new(value.Where(char.IsDigit).ToArray());

    public static string NormalizePhone(string value)
    {
        var digits = NormalizeDigits(value);
        if (digits.StartsWith("90", StringComparison.Ordinal) && digits.Length == 12)
        {
            return digits;
        }
        if (digits.StartsWith('0') && digits.Length == 11)
        {
            return "9" + digits;
        }
        if (digits.Length == 10)
        {
            return "90" + digits;
        }
        return digits;
    }

    public static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();
}

public interface ITcknValidator
{
    bool IsValid(string nationalId);
}

public sealed class TcknValidator : ITcknValidator
{
    public bool IsValid(string nationalId)
    {
        var digits = Normalization.NormalizeDigits(nationalId);
        if (digits.Length != 11 || digits[0] == '0')
        {
            return false;
        }

        var values = digits.Select(x => x - '0').ToArray();
        var oddSum = values[0] + values[2] + values[4] + values[6] + values[8];
        var evenSum = values[1] + values[3] + values[5] + values[7];
        var tenth = ((oddSum * 7) - evenSum) % 10;
        var eleventh = values.Take(10).Sum() % 10;
        return values[9] == tenth && values[10] == eleventh;
    }
}

public interface IDataProtectionKeyProvider
{
    byte[] EncryptionKey { get; }
    byte[] LookupKey { get; }
}

public sealed class ConfigurationDataProtectionKeyProvider : IDataProtectionKeyProvider
{
    public ConfigurationDataProtectionKeyProvider(IConfiguration configuration, IWebHostEnvironment environment)
    {
        EncryptionKey = ResolveKey(configuration["Security:EncryptionKey"], "development-only-encryption-key", environment, "Security:EncryptionKey");
        LookupKey = ResolveKey(configuration["Security:LookupKey"], "development-only-lookup-key", environment, "Security:LookupKey");
    }

    public byte[] EncryptionKey { get; }
    public byte[] LookupKey { get; }

    private static byte[] ResolveKey(string? configuredValue, string developmentValue, IWebHostEnvironment environment, string name)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException($"{name} is required in production.");
            }
            configuredValue = developmentValue;
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(configuredValue));
    }
}

public interface ICryptoService
{
    string Encrypt(string plaintext);
    string Decrypt(string protectedValue);
    string HashLookup(string normalizedValue);
}

public sealed class CryptoService(IDataProtectionKeyProvider keys) : ICryptoService
{
    public string Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plaintextBytes.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(keys.EncryptionKey, 16);
        aes.Encrypt(nonce, plaintextBytes, cipher, tag);

        var output = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, output, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, output, nonce.Length + tag.Length, cipher.Length);
        return Convert.ToBase64String(output);
    }

    public string Decrypt(string protectedValue)
    {
        var input = Convert.FromBase64String(protectedValue);
        var nonce = input[..12];
        var tag = input[12..28];
        var cipher = input[28..];
        var plaintext = new byte[cipher.Length];
        using var aes = new AesGcm(keys.EncryptionKey, 16);
        aes.Decrypt(nonce, cipher, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    public string HashLookup(string normalizedValue)
    {
        using var hmac = new HMACSHA256(keys.LookupKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(normalizedValue));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public interface IMaskingService
{
    string MaskName(string firstName, string lastName);
    string MaskHashOnly(string hash);
}

public sealed class MaskingService : IMaskingService
{
    public string MaskName(string firstName, string lastName)
    {
        static string MaskPart(string value)
        {
            value = value.Trim();
            if (value.Length <= 1)
            {
                return "*";
            }
            return $"{value[0]}{new string('*', Math.Min(4, value.Length - 1))}";
        }

        return $"{MaskPart(firstName)} {MaskPart(lastName)}";
    }

    public string MaskHashOnly(string hash) => hash.Length <= 8 ? "********" : $"hash:{hash[..4]}****{hash[^4..]}";
}

public interface IExportSanitizer
{
    string SanitizeCell(string value);
}

public sealed class ExportSanitizer : IExportSanitizer
{
    private static readonly char[] DangerousPrefixes = ['=', '+', '-', '@', '\t', '\r', '\n'];

    public string SanitizeCell(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        var trimmedStart = value.TrimStart();
        return trimmedStart.Length > 0 && DangerousPrefixes.Contains(trimmedStart[0])
            ? "'" + value
            : value;
    }
}

public interface ITokenService
{
    TokenPair CreateAccessAndRefreshToken(AdminUser user, IReadOnlyCollection<string> permissions);
    string HashToken(string token);
}

public sealed record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt, DateTimeOffset RefreshTokenExpiresAt);

public sealed class TokenService(IConfiguration configuration, IWebHostEnvironment environment, ICryptoService crypto, ISystemClock clock) : ITokenService
{
    public const string Issuer = "BasvuruAkis";
    public const string Audience = "BasvuruAkis.Admin";

    private readonly SymmetricSecurityKey _signingKey = ResolveSigningKey(configuration, environment);

    public static SymmetricSecurityKey ResolveSigningKey(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException("Jwt:SigningKey is required in production.");
            }
            configured = "development-only-jwt-signing-key";
        }

        return new SymmetricSecurityKey(SHA256.HashData(Encoding.UTF8.GetBytes(configured)));
    }

    public TokenPair CreateAccessAndRefreshToken(AdminUser user, IReadOnlyCollection<string> permissions)
    {
        var now = clock.UtcNow;
        var expires = now.AddMinutes(15);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email)
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(Issuer, Audience, claims, now.UtcDateTime, expires.UtcDateTime, credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Base64Url(RandomNumberGenerator.GetBytes(48));
        return new TokenPair(accessToken, refreshToken, expires, now.AddDays(14));
    }

    public string HashToken(string token) => crypto.HashLookup(token);

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

public interface ITotpService
{
    bool Verify(string secret, string code, DateTimeOffset now);
}

public sealed class TotpService : ITotpService
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public bool Verify(string secret, string code, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6 || code.Any(x => !char.IsDigit(x)))
        {
            return false;
        }

        secret = NormalizeSecret(secret);
        var timestep = now.ToUnixTimeSeconds() / 30;
        return Enumerable.Range(-1, 3).Any(offset => Generate(secret, timestep + offset) == code);
    }

    public static string NormalizeSecret(string secret)
    {
        var normalized = new string(secret.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        if (normalized.Length < 16 || normalized.Any(ch => Base32Alphabet.IndexOf(ch) < 0))
        {
            throw new InvalidOperationException("TOTP secret must be a Base32 value with at least 16 characters.");
        }

        _ = DecodeBase32(normalized);
        return normalized;
    }

    private static string Generate(string secret, long timestep)
    {
        var key = DecodeBase32(secret);
        var counter = BitConverter.GetBytes(timestep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0xf;
        var binary =
            ((hash[offset] & 0x7f) << 24) |
            ((hash[offset + 1] & 0xff) << 16) |
            ((hash[offset + 2] & 0xff) << 8) |
            (hash[offset + 3] & 0xff);
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] DecodeBase32(string value)
    {
        var bits = 0;
        var bitBuffer = 0;
        var output = new List<byte>();
        foreach (var ch in value.TrimEnd('='))
        {
            var index = Base32Alphabet.IndexOf(ch);
            if (index < 0)
            {
                throw new InvalidOperationException("TOTP secret must be Base32 encoded.");
            }

            bitBuffer = (bitBuffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((bitBuffer >> (bits - 8)) & 0xff));
                bits -= 8;
            }
        }

        if (output.Count < 10)
        {
            throw new InvalidOperationException("TOTP secret must decode to at least 10 bytes.");
        }

        return output.ToArray();
    }
}

public interface IAuditService
{
    Task WriteAsync(Guid? actorUserId, string action, string entityType, string? entityId, object metadata, CancellationToken cancellationToken);
}

public sealed class AuditService(AppDbContext db, ISystemClock clock) : IAuditService
{
    public async Task WriteAsync(Guid? actorUserId, string action, string entityType, string? entityId, object metadata, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            MetadataJson = JsonSerializer.Serialize(metadata),
            CreatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}

public interface ISecurityLogService
{
    Task WriteAsync(string eventType, Guid? actorUserId, string ipAddress, string userAgent, object metadata, CancellationToken cancellationToken);
}

public sealed class SecurityLogService(AppDbContext db, ISystemClock clock) : ISecurityLogService
{
    public async Task WriteAsync(string eventType, Guid? actorUserId, string ipAddress, string userAgent, object metadata, CancellationToken cancellationToken)
    {
        db.SecurityLogs.Add(new SecurityLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            EventType = eventType,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            MetadataJson = JsonSerializer.Serialize(metadata),
            CreatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}

public static class AuditActions
{
    public const string LoginSucceeded = "login.succeeded";
    public const string LoginFailed = "login.failed";
    public const string TokenRefreshed = "token.refreshed";
    public const string Logout = "logout";
    public const string ApplicationViewed = "application.viewed";
    public const string ApplicationAssigned = "application.assigned";
    public const string ExportCreated = "export.created";
}

public static class Permissions
{
    public const string ApplicationsRead = "applications.read";
    public const string ApplicationsDetailRead = "applications.detail.read";
    public const string ApplicationsAssign = "applications.assign";
    public const string ApplicationsAnonymize = "applications.anonymize";
    public const string ApplicationsExport = "applications.export";
    public const string DashboardRead = "dashboard.read";
    public const string AuditRead = "audit.read";
    public const string ContentManage = "content.manage";
    public const string LegalTextManage = "legal-text.manage";
    public const string SystemManage = "system.manage";

    public static readonly string[] SuperAdmin =
    [
        ApplicationsRead,
        ApplicationsDetailRead,
        ApplicationsAssign,
        ApplicationsAnonymize,
        ApplicationsExport,
        DashboardRead,
        AuditRead,
        ContentManage,
        LegalTextManage,
        SystemManage
    ];
}

public static class ClaimsPrincipalExtensions
{
    public static bool HasPermission(this ClaimsPrincipal user, string permission) =>
        user.Claims.Any(x => x.Type == "permission" && x.Value == permission);

    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}

public static class HttpContextExtensions
{
    public static string GetClientIp(this HttpContext context) =>
        context.Request.Headers.TryGetValue("CF-Connecting-IP", out var cloudflareIp)
            ? cloudflareIp.ToString()
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

public static class SecurityHeadersMiddleware
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
            context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            context.Response.Headers.TryAdd("Content-Security-Policy", "default-src 'self'; frame-ancestors 'none'");
            await next();
        });
    }
}

public sealed record ServiceResult<T>(bool Success, T? Value, string ErrorCode, string Message)
{
    public static ServiceResult<T> Ok(T value) => new(true, value, "", "");
    public static ServiceResult<T> Fail(string code, string message) => new(false, default, code, message);
}

public sealed record ServiceResult(bool Success, string ErrorCode, string Message)
{
    public static ServiceResult Ok() => new(true, "", "");
    public static ServiceResult Fail(string code, string message) => new(false, code, message);
}
