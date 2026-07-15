using BasvuruAkis.Api.Domain;
using BasvuruAkis.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BasvuruAkis.Api.Data;

public static class ProductionBootstrap
{
    private const string HttpJsonSmsProvider = "http-json";

    public static async Task EnsureAdminAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var environment = services.GetRequiredService<IWebHostEnvironment>();
        if (!environment.IsProduction())
        {
            return;
        }

        var db = services.GetRequiredService<AppDbContext>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var clock = services.GetRequiredService<ISystemClock>();

        ValidateExternalIntegrations(configuration);

        if (!await db.AdminUsers.AnyAsync(cancellationToken))
        {
            var email = Required(configuration, "AdminBootstrap:Email");
            var password = Required(configuration, "AdminBootstrap:Password");
            var mfaSecret = Required(configuration, "AdminBootstrap:MfaSecret");

            ValidateEmail(email);
            ValidatePassword(password);
            mfaSecret = TotpService.NormalizeSecret(mfaSecret);

            var adminId = Guid.NewGuid();
            var admin = new AdminUser
            {
                Id = adminId,
                Email = Normalization.NormalizeEmail(email),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
                MfaEnabled = true,
                MfaSecret = mfaSecret,
                CreatedAt = clock.UtcNow,
                Permissions = Permissions.SuperAdmin.Select(permission => new AdminUserPermission
                {
                    AdminUserId = adminId,
                    Permission = permission
                }).ToList()
            };
            db.AdminUsers.Add(admin);
            await db.SaveChangesAsync(cancellationToken);
        }

        var hasMfaDisabledAdmin = await db.AdminUsers.AnyAsync(
            user => !user.MfaEnabled || string.IsNullOrWhiteSpace(user.MfaSecret),
            cancellationToken);
        if (hasMfaDisabledAdmin)
        {
            throw new InvalidOperationException("Production admin users must have MFA enabled.");
        }

        var admins = await db.AdminUsers.AsNoTracking()
            .Where(user => user.MfaEnabled)
            .Select(user => new { user.Email, user.MfaSecret })
            .ToListAsync(cancellationToken);
        foreach (var admin in admins)
        {
            try
            {
                _ = TotpService.NormalizeSecret(admin.MfaSecret!);
            }
            catch (InvalidOperationException error)
            {
                throw new InvalidOperationException($"Production admin MFA secret is invalid for {admin.Email}.", error);
            }
        }
    }

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is required in production.");
        }

        return value.Trim();
    }

    private static void ValidateExternalIntegrations(IConfiguration configuration)
    {
        _ = Required(configuration, "Captcha:TurnstileSecret");

        var smsProvider = Required(configuration, "Sms:Provider");
        if (!smsProvider.Equals(HttpJsonSmsProvider, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Sms:Provider must be {HttpJsonSmsProvider} in production.");
        }

        _ = Required(configuration, "Sms:ApiKey");
        var endpoint = Required(configuration, "Sms:Endpoint");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) || endpointUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Sms:Endpoint must be an absolute HTTPS URL in production.");
        }

        var messageTemplate = configuration["Sms:MessageTemplate"];
        if (!string.IsNullOrWhiteSpace(messageTemplate) && !messageTemplate.Contains("{code}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Sms:MessageTemplate must include the {code} placeholder.");
        }
    }

    private static void ValidateEmail(string email)
    {
        if (!email.Contains('@', StringComparison.Ordinal) || email.Length > 320)
        {
            throw new InvalidOperationException("AdminBootstrap:Email must be a valid email address.");
        }
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 14 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            throw new InvalidOperationException("AdminBootstrap:Password must be at least 14 characters and include uppercase, lowercase, digit and symbol characters.");
        }
    }
}
