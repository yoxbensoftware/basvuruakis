using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BasvuruAkis.Api.Data;
using BasvuruAkis.Api.Domain;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BasvuruAkis.Api.Services;

public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string token, CancellationToken cancellationToken);
}

public sealed class CaptchaVerifier(IConfiguration configuration, IWebHostEnvironment environment, IHttpClientFactory httpClientFactory) : ICaptchaVerifier
{
    public async Task<bool> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (!environment.IsProduction())
        {
            return !token.Equals("fail", StringComparison.OrdinalIgnoreCase);
        }

        var secret = configuration["Captcha:TurnstileSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("Captcha:TurnstileSecret is required in production.");
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["secret"] = secret,
            ["response"] = token
        });
        var client = httpClientFactory.CreateClient();
        using var response = await client.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.TryGetProperty("success", out var success) && success.GetBoolean();
    }
}

public interface ISmsProvider
{
    Task SendOtpAsync(string normalizedPhone, string code, CancellationToken cancellationToken);
}

public sealed class SmsProvider(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    IHttpClientFactory httpClientFactory,
    ILogger<SmsProvider> logger) : ISmsProvider
{
    private const string HttpJsonProvider = "http-json";

    public async Task SendOtpAsync(string normalizedPhone, string code, CancellationToken cancellationToken)
    {
        if (!environment.IsProduction())
        {
            logger.LogInformation("Development SMS OTP generated for phone hash target. Code is available only in API response for non-production.");
            return;
        }

        var provider = Required("Sms:Provider");
        if (!provider.Equals(HttpJsonProvider, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported production SMS provider '{provider}'. Supported provider: {HttpJsonProvider}.");
        }

        var apiKey = Required("Sms:ApiKey");
        var endpoint = Required("Sms:Endpoint");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) || endpointUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Sms:Endpoint must be an absolute HTTPS URL in production.");
        }

        var template = configuration["Sms:MessageTemplate"];
        if (string.IsNullOrWhiteSpace(template))
        {
            template = "Basvuru dogrulama kodunuz: {code}";
        }
        if (!template.Contains("{code}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Sms:MessageTemplate must include the {code} placeholder.");
        }

        var timeoutSeconds = Math.Clamp(configuration.GetValue("Sms:TimeoutSeconds", 10), 1, 30);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var client = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpointUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(new SmsSendRequest(
            normalizedPhone,
            template.Replace("{code}", code, StringComparison.Ordinal),
            configuration["Sms:Sender"]?.Trim()));

        using var response = await client.SendAsync(request, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"SMS provider call failed with status {(int)response.StatusCode}.");
        }

        logger.LogInformation("Production SMS provider accepted OTP message.");
    }

    private string Required(string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is required in production.");
        }

        return value.Trim();
    }

    private sealed record SmsSendRequest(string To, string Message, string? Sender);
}

public interface IOtpService
{
    Task<ServiceResult<OtpRequestResponse>> RequestAsync(OtpRequestDto request, OtpRequestContext context, CancellationToken cancellationToken);
    Task<ServiceResult<OtpVerifyResponse>> VerifyAsync(OtpVerifyDto request, OtpRequestContext context, CancellationToken cancellationToken);
    Task<bool> ConsumeVerificationTokenAsync(string normalizedPhone, string verificationToken, CancellationToken cancellationToken);
}

public sealed class OtpService(
    AppDbContext db,
    IConfiguration configuration,
    ICaptchaVerifier captchaVerifier,
    ISmsProvider smsProvider,
    ICryptoService crypto,
    ISystemClock clock,
    IWebHostEnvironment environment,
    ISecurityLogService securityLog) : IOtpService
{
    public async Task<ServiceResult<OtpRequestResponse>> RequestAsync(OtpRequestDto request, OtpRequestContext context, CancellationToken cancellationToken)
    {
        var ipAddress = NormalizeContextValue(context.IpAddress, 64, "unknown");
        var userAgent = NormalizeContextValue(context.UserAgent, 256, "");
        var deviceId = NormalizeContextValue(request.DeviceId, 128, "unknown");

        if (!await captchaVerifier.VerifyAsync(request.CaptchaToken, cancellationToken))
        {
            await securityLog.WriteAsync("captcha.failed", null, ipAddress, userAgent, new { purpose = "otp", deviceId }, cancellationToken);
            return ServiceResult<OtpRequestResponse>.Fail("captcha_failed", "CAPTCHA doğrulaması başarısız.");
        }

        var phone = Normalization.NormalizePhone(request.Phone);
        if (phone.Length < 10)
        {
            return ServiceResult<OtpRequestResponse>.Fail("invalid_phone", "Telefon formatı geçerli değil.");
        }

        var now = clock.UtcNow;
        var phoneHash = crypto.HashLookup(phone);
        var last = await db.OtpRequests
            .Where(x => x.PhoneHash == phoneHash)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (last is not null && last.ResendAvailableAt > now)
        {
            await securityLog.WriteAsync("otp.cooldown", null, ipAddress, userAgent, new { deviceId }, cancellationToken);
            return ServiceResult<OtpRequestResponse>.Fail("otp_cooldown", "Yeni kod istemek için bekleme süresi dolmalı.");
        }

        var since = now.AddHours(-1);
        var maxRequestsPerIpPerHour = Math.Max(1, configuration.GetValue("Otp:MaxRequestsPerIpPerHour", 20));
        var maxRequestsPerDevicePerHour = Math.Max(1, configuration.GetValue("Otp:MaxRequestsPerDevicePerHour", 10));
        var ipRequestCount = await db.OtpRequests.CountAsync(x => x.IpAddress == ipAddress && x.CreatedAt >= since, cancellationToken);
        if (ipRequestCount >= maxRequestsPerIpPerHour)
        {
            await securityLog.WriteAsync("otp.rate_limit.ip", null, ipAddress, userAgent, new { count = ipRequestCount, limit = maxRequestsPerIpPerHour }, cancellationToken);
            return ServiceResult<OtpRequestResponse>.Fail("otp_ip_rate_limited", "Çok fazla doğrulama kodu istendi. Lütfen daha sonra tekrar deneyin.");
        }

        if (deviceId != "unknown")
        {
            var deviceRequestCount = await db.OtpRequests.CountAsync(x => x.DeviceId == deviceId && x.CreatedAt >= since, cancellationToken);
            if (deviceRequestCount >= maxRequestsPerDevicePerHour)
            {
                await securityLog.WriteAsync("otp.rate_limit.device", null, ipAddress, userAgent, new { deviceId, count = deviceRequestCount, limit = maxRequestsPerDevicePerHour }, cancellationToken);
                return ServiceResult<OtpRequestResponse>.Fail("otp_device_rate_limited", "Çok fazla doğrulama kodu istendi. Lütfen daha sonra tekrar deneyin.");
            }
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var otp = new OtpRequest
        {
            Id = Guid.NewGuid(),
            PhoneHash = phoneHash,
            CodeHash = crypto.HashLookup($"{phone}:{code}"),
            Attempts = 0,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(3),
            ResendAvailableAt = now.AddSeconds(60),
            DeviceId = deviceId,
            IpAddress = ipAddress
        };
        db.OtpRequests.Add(otp);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await smsProvider.SendOtpAsync(phone, code, cancellationToken);
        }
        catch (Exception error) when (error is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            db.OtpRequests.Remove(otp);
            await db.SaveChangesAsync(cancellationToken);
            await securityLog.WriteAsync("otp.sms.failed", null, ipAddress, userAgent, new { deviceId }, cancellationToken);
            return ServiceResult<OtpRequestResponse>.Fail("sms_delivery_failed", "Doğrulama kodu gönderilemedi. Lütfen daha sonra tekrar deneyin.");
        }

        return ServiceResult<OtpRequestResponse>.Ok(new OtpRequestResponse(
            otp.Id,
            otp.ExpiresAt,
            otp.ResendAvailableAt,
            environment.IsProduction() ? null : code));
    }

    public async Task<ServiceResult<OtpVerifyResponse>> VerifyAsync(OtpVerifyDto request, OtpRequestContext context, CancellationToken cancellationToken)
    {
        var phone = Normalization.NormalizePhone(request.Phone);
        var now = clock.UtcNow;
        var ipAddress = NormalizeContextValue(context.IpAddress, 64, "unknown");
        var userAgent = NormalizeContextValue(context.UserAgent, 256, "");
        var deviceId = NormalizeContextValue(request.DeviceId, 128, "unknown");
        var phoneHash = crypto.HashLookup(phone);
        var otp = await db.OtpRequests
            .Where(x => x.PhoneHash == phoneHash && x.VerifiedAt == null && x.VerificationTokenUsedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null || otp.ExpiresAt < now)
        {
            await securityLog.WriteAsync("otp.verify.failed", null, ipAddress, userAgent, new { deviceId, reason = "expired_or_missing" }, cancellationToken);
            return ServiceResult<OtpVerifyResponse>.Fail("otp_expired", "OTP süresi doldu veya kod bulunamadı.");
        }
        if (otp.Attempts >= 5)
        {
            await securityLog.WriteAsync("otp.verify.limit", null, ipAddress, userAgent, new { deviceId, otpRequestId = otp.Id }, cancellationToken);
            return ServiceResult<OtpVerifyResponse>.Fail("otp_attempt_limit", "OTP deneme limiti aşıldı.");
        }

        otp.Attempts += 1;
        var expectedHash = crypto.HashLookup($"{phone}:{request.Code}");
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedHash), Encoding.UTF8.GetBytes(otp.CodeHash)))
        {
            await securityLog.WriteAsync("otp.verify.failed", null, ipAddress, userAgent, new { deviceId, otpRequestId = otp.Id, reason = "invalid_code" }, cancellationToken);
            return ServiceResult<OtpVerifyResponse>.Fail("otp_invalid", "OTP kodu geçerli değil.");
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        otp.VerifiedAt = now;
        otp.VerificationTokenHash = crypto.HashLookup(token);
        otp.VerificationTokenExpiresAt = now.AddMinutes(10);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<OtpVerifyResponse>.Ok(new OtpVerifyResponse(token, otp.VerificationTokenExpiresAt.Value));
    }

    private static string NormalizeContextValue(string? value, int maxLength, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    public async Task<bool> ConsumeVerificationTokenAsync(string normalizedPhone, string verificationToken, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tokenHash = crypto.HashLookup(verificationToken);
        var phoneHash = crypto.HashLookup(normalizedPhone);
        var otp = await db.OtpRequests
            .Where(x =>
                x.PhoneHash == phoneHash &&
                x.VerificationTokenHash == tokenHash &&
                x.VerificationTokenUsedAt == null &&
                x.VerificationTokenExpiresAt >= now)
            .OrderByDescending(x => x.VerifiedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null)
        {
            return false;
        }

        otp.VerificationTokenUsedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public interface IAssignmentService
{
    Task AssignAutomaticallyAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<ServiceResult> AssignManuallyAsync(Guid applicationId, int representativeOfficeId, Guid actorUserId, string reason, CancellationToken cancellationToken);
}

public sealed class AssignmentService(AppDbContext db, ISystemClock clock, IAuditService audit) : IAssignmentService
{
    public async Task AssignAutomaticallyAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await db.Applications.FirstAsync(x => x.Id == applicationId, cancellationToken);
        var province = await db.Provinces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == application.ProvinceId, cancellationToken);
        var now = clock.UtcNow;

        var candidates = await db.AssignmentRules.AsNoTracking()
            .Where(x => x.IsActive && (x.ValidFrom == null || x.ValidFrom <= now) && (x.ValidUntil == null || x.ValidUntil >= now))
            .ToListAsync(cancellationToken);

        var orderedScopes = new (AssignmentRuleScope Scope, int? ScopeId)[]
        {
            (AssignmentRuleScope.Neighborhood, application.NeighborhoodId),
            (AssignmentRuleScope.District, application.DistrictId),
            (AssignmentRuleScope.Province, application.ProvinceId),
            (AssignmentRuleScope.Region, province?.RegionId),
            (AssignmentRuleScope.Default, null)
        };

        AssignmentRule? rule = null;
        foreach (var (scope, scopeId) in orderedScopes)
        {
            rule = candidates
                .Where(x => x.Scope == scope && x.ScopeId == scopeId)
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.CreatedAt)
                .FirstOrDefault();
            if (rule is not null)
            {
                break;
            }
        }

        var officeId = rule?.RepresentativeOfficeId
            ?? await db.RepresentativeOffices.Where(x => x.IsDefault && x.IsActive).Select(x => x.Id).FirstAsync(cancellationToken);

        db.ApplicationAssignments.Add(new ApplicationAssignment
        {
            Id = Guid.NewGuid(),
            ApplicationId = application.Id,
            RepresentativeOfficeId = officeId,
            AssignmentRuleId = rule?.Id,
            IsAutomatic = true,
            CreatedAt = now
        });
        application.Status = ApplicationStatus.Assigned;
        db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = application.Id,
            FromStatus = ApplicationStatus.Submitted,
            ToStatus = ApplicationStatus.Assigned,
            Reason = "automatic_assignment",
            CreatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ServiceResult> AssignManuallyAsync(Guid applicationId, int representativeOfficeId, Guid actorUserId, string reason, CancellationToken cancellationToken)
    {
        var application = await db.Applications.FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken);
        if (application is null)
        {
            return ServiceResult.Fail("not_found", "Başvuru bulunamadı.");
        }

        var officeExists = await db.RepresentativeOffices.AnyAsync(x => x.Id == representativeOfficeId && x.IsActive, cancellationToken);
        if (!officeExists)
        {
            return ServiceResult.Fail("office_not_found", "Aktif temsilcilik bulunamadı.");
        }

        var previousRepresentativeOfficeId = await db.ApplicationAssignments
            .Where(x => x.ApplicationId == applicationId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.IsAutomatic)
            .Select(x => (int?)x.RepresentativeOfficeId)
            .FirstOrDefaultAsync(cancellationToken);
        var now = clock.UtcNow;
        var oldStatus = application.Status;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.ApplicationAssignments.Add(new ApplicationAssignment
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            RepresentativeOfficeId = representativeOfficeId,
            IsAutomatic = false,
            ActorUserId = actorUserId,
            Reason = reason,
            CreatedAt = now
        });
        if (application.Status != ApplicationStatus.Assigned)
        {
            application.Status = ApplicationStatus.Assigned;
            db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                Id = Guid.NewGuid(),
                ApplicationId = applicationId,
                FromStatus = oldStatus,
                ToStatus = ApplicationStatus.Assigned,
                ActorUserId = actorUserId,
                Reason = reason,
                CreatedAt = now
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(
            actorUserId,
            AuditActions.ApplicationAssigned,
            nameof(ApplicationRecord),
            applicationId.ToString(),
            new { previousRepresentativeOfficeId, representativeOfficeId, reason },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult.Ok();
    }
}

public interface IAnonymizationService
{
    Task<ServiceResult<ApplicationAnonymizedResponse>> AnonymizeApplicationAsync(Guid applicationId, Guid actorUserId, string reason, bool confirmed, CancellationToken cancellationToken);
}

public sealed class AnonymizationService(AppDbContext db, ICryptoService crypto, ISystemClock clock, IAuditService audit) : IAnonymizationService
{
    public async Task<ServiceResult<ApplicationAnonymizedResponse>> AnonymizeApplicationAsync(Guid applicationId, Guid actorUserId, string reason, bool confirmed, CancellationToken cancellationToken)
    {
        if (!confirmed)
        {
            return ServiceResult<ApplicationAnonymizedResponse>.Fail("confirmation_required", "Anonimleştirme için açık onay zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
        {
            return ServiceResult<ApplicationAnonymizedResponse>.Fail("reason_required", "Anonimleştirme gerekçesi zorunludur.");
        }

        var application = await db.Applications.FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken);
        if (application is null)
        {
            return ServiceResult<ApplicationAnonymizedResponse>.Fail("not_found", "Başvuru bulunamadı.");
        }

        if (application.Status == ApplicationStatus.Anonymized)
        {
            return ServiceResult<ApplicationAnonymizedResponse>.Fail("already_anonymized", "Başvuru zaten anonimleştirilmiş.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var now = clock.UtcNow;
        var oldStatus = application.Status;
        var oldPhoneHash = application.PhoneHash;

        application.FirstName = "Anonim";
        application.LastName = "Kayıt";
        application.NationalIdEncrypted = crypto.Encrypt("");
        application.NationalIdHash = $"anon-national-{application.Id:N}";
        application.PhoneEncrypted = crypto.Encrypt("");
        application.PhoneHash = $"anon-phone-{application.Id:N}";
        application.EmailEncrypted = crypto.Encrypt("");
        application.EmailHash = $"anon-email-{application.Id:N}";
        application.AddressEncrypted = crypto.Encrypt("");
        application.PostalCode = null;
        application.Status = ApplicationStatus.Anonymized;
        application.AnonymizedAt = now;

        var consents = await db.ApplicationConsents.Where(x => x.ApplicationId == applicationId).ToListAsync(cancellationToken);
        foreach (var consent in consents)
        {
            consent.IpAddress = "anonymized";
            consent.UserAgent = "anonymized";
        }

        var otpRequests = await db.OtpRequests.Where(x => x.PhoneHash == oldPhoneHash).ToListAsync(cancellationToken);
        foreach (var otp in otpRequests)
        {
            otp.PhoneHash = $"anon-otp-{otp.Id:N}";
            otp.CodeHash = "anonymized";
            otp.VerificationTokenHash = null;
            otp.IpAddress = "anonymized";
            otp.DeviceId = "anonymized";
        }

        db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            FromStatus = oldStatus,
            ToStatus = ApplicationStatus.Anonymized,
            ActorUserId = actorUserId,
            Reason = reason.Trim(),
            CreatedAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(actorUserId, "application.anonymized", nameof(ApplicationRecord), applicationId.ToString(), new { reason = reason.Trim() }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<ApplicationAnonymizedResponse>.Ok(new ApplicationAnonymizedResponse(application.Id, application.Status.ToString(), now));
    }
}

public interface IAuthService
{
    Task<ServiceResult<LoginResponse>> LoginAsync(AdminLoginRequest request, string ipAddress, string userAgent, CancellationToken cancellationToken);
    Task<ServiceResult<LoginResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken);
}

public sealed class AuthService(
    AppDbContext db,
    ITokenService tokenService,
    ITotpService totpService,
    ISystemClock clock,
    IAuditService audit,
    ISecurityLogService securityLog) : IAuthService
{
    public async Task<ServiceResult<LoginResponse>> LoginAsync(AdminLoginRequest request, string ipAddress, string userAgent, CancellationToken cancellationToken)
    {
        var email = Normalization.NormalizeEmail(request.Email);
        var user = await db.AdminUsers.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null)
        {
            await securityLog.WriteAsync(AuditActions.LoginFailed, null, ipAddress, userAgent, new { email }, cancellationToken);
            return ServiceResult<LoginResponse>.Fail("invalid_credentials", "Geçersiz kimlik bilgileri.");
        }

        var now = clock.UtcNow;
        if (user.LockoutUntil is not null && user.LockoutUntil > now)
        {
            await securityLog.WriteAsync("login.locked", user.Id, ipAddress, userAgent, new { email }, cancellationToken);
            return ServiceResult<LoginResponse>.Fail("locked", "Hesap geçici olarak kilitli.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount += 1;
            if (user.FailedLoginCount >= 5)
            {
                user.LockoutUntil = now.AddMinutes(15);
            }
            await db.SaveChangesAsync(cancellationToken);
            await securityLog.WriteAsync(AuditActions.LoginFailed, user.Id, ipAddress, userAgent, new { email }, cancellationToken);
            return ServiceResult<LoginResponse>.Fail("invalid_credentials", "Geçersiz kimlik bilgileri.");
        }

        if (user.MfaEnabled && (string.IsNullOrWhiteSpace(request.TotpCode) || !totpService.Verify(user.MfaSecret!, request.TotpCode, now)))
        {
            user.FailedLoginCount += 1;
            if (user.FailedLoginCount >= 5)
            {
                user.LockoutUntil = now.AddMinutes(15);
            }
            await db.SaveChangesAsync(cancellationToken);
            await securityLog.WriteAsync("login.mfa_failed", user.Id, ipAddress, userAgent, new { email }, cancellationToken);
            return ServiceResult<LoginResponse>.Fail("mfa_required", "MFA kodu gerekli veya geçersiz.");
        }

        user.FailedLoginCount = 0;
        user.LockoutUntil = null;
        var permissions = user.Permissions.Select(x => x.Permission).Order().ToArray();
        var pair = tokenService.CreateAccessAndRefreshToken(user, permissions);
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            AdminUserId = user.Id,
            TokenHash = tokenService.HashToken(pair.RefreshToken),
            CreatedAt = now,
            ExpiresAt = pair.RefreshTokenExpiresAt
        });
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(user.Id, AuditActions.LoginSucceeded, nameof(AdminUser), user.Id.ToString(), new { user.Email }, cancellationToken);

        return ServiceResult<LoginResponse>.Ok(new LoginResponse(pair.AccessToken, pair.RefreshToken, pair.AccessTokenExpiresAt, pair.RefreshTokenExpiresAt, permissions));
    }

    public async Task<ServiceResult<LoginResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tokenHash = tokenService.HashToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt < now)
        {
            return ServiceResult<LoginResponse>.Fail("invalid_refresh", "Refresh token geçersiz.");
        }

        var user = await db.AdminUsers.Include(x => x.Permissions).FirstAsync(x => x.Id == stored.AdminUserId, cancellationToken);
        var permissions = user.Permissions.Select(x => x.Permission).Order().ToArray();
        var pair = tokenService.CreateAccessAndRefreshToken(user, permissions);
        stored.RevokedAt = now;
        stored.ReplacedByTokenHash = tokenService.HashToken(pair.RefreshToken);
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            AdminUserId = user.Id,
            TokenHash = stored.ReplacedByTokenHash,
            CreatedAt = now,
            ExpiresAt = pair.RefreshTokenExpiresAt
        });
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(user.Id, AuditActions.TokenRefreshed, nameof(AdminUser), user.Id.ToString(), new { user.Email }, cancellationToken);
        return ServiceResult<LoginResponse>.Ok(new LoginResponse(pair.AccessToken, pair.RefreshToken, pair.AccessTokenExpiresAt, pair.RefreshTokenExpiresAt, permissions));
    }

    public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken)
    {
        var hash = tokenService.HashToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(x => x.AdminUserId == userId && x.TokenHash == hash, cancellationToken);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        await audit.WriteAsync(userId, AuditActions.Logout, nameof(AdminUser), userId.ToString(), new { }, cancellationToken);
    }
}

public interface IExportService
{
    Task<ServiceResult<ExportFile>> ExportApplicationsAsync(Guid? actorUserId, ExportRequest request, CancellationToken cancellationToken);
}

public static class ApplicationQueryFilter
{
    public static IQueryable<ApplicationRecord> Apply(
        IQueryable<ApplicationRecord> applications,
        AppDbContext db,
        ICryptoService crypto,
        ApplicationQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<ApplicationStatus>(query.Status, true, out var status))
        {
            applications = applications.Where(x => x.Status == status);
        }

        var firstName = NormalizeTextSearch(query.FirstName);
        if (firstName is not null)
        {
            applications = applications.Where(x => x.FirstName.ToLower().Contains(firstName));
        }

        var lastName = NormalizeTextSearch(query.LastName);
        if (lastName is not null)
        {
            applications = applications.Where(x => x.LastName.ToLower().Contains(lastName));
        }

        var nationalId = Normalization.NormalizeDigits(query.NationalId ?? "");
        if (!string.IsNullOrWhiteSpace(nationalId))
        {
            var hash = crypto.HashLookup(nationalId);
            applications = applications.Where(x => x.NationalIdHash == hash);
        }

        var phone = Normalization.NormalizePhone(query.Phone ?? "");
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var hash = crypto.HashLookup(phone);
            applications = applications.Where(x => x.PhoneHash == hash);
        }

        if (!string.IsNullOrWhiteSpace(query.Email))
        {
            var hash = crypto.HashLookup(Normalization.NormalizeEmail(query.Email));
            applications = applications.Where(x => x.EmailHash == hash);
        }

        if (query.ProvinceId is not null)
        {
            applications = applications.Where(x => x.ProvinceId == query.ProvinceId);
        }
        if (query.DistrictId is not null)
        {
            applications = applications.Where(x => x.DistrictId == query.DistrictId);
        }
        if (query.NeighborhoodId is not null)
        {
            applications = applications.Where(x => x.NeighborhoodId == query.NeighborhoodId);
        }
        if (query.IsPhoneVerified is not null)
        {
            applications = applications.Where(x => x.IsPhoneVerified == query.IsPhoneVerified);
        }
        if (query.IsAssigned is not null)
        {
            applications = query.IsAssigned.Value
                ? applications.Where(x => db.ApplicationAssignments.Any(a => a.ApplicationId == x.Id))
                : applications.Where(x => !db.ApplicationAssignments.Any(a => a.ApplicationId == x.Id));
        }
        if (query.RepresentativeOfficeId is not null)
        {
            applications = applications.Where(x =>
                db.ApplicationAssignments
                    .Where(a => a.ApplicationId == x.Id)
                    .OrderByDescending(a => a.CreatedAt)
                    .ThenBy(a => a.IsAutomatic)
                    .Select(a => (int?)a.RepresentativeOfficeId)
                    .FirstOrDefault() == query.RepresentativeOfficeId);
        }
        if (query.From is not null)
        {
            applications = applications.Where(x => x.CreatedAt >= query.From);
        }
        if (query.To is not null)
        {
            applications = applications.Where(x => x.CreatedAt <= query.To);
        }

        return applications;
    }

    private static string? NormalizeTextSearch(string? value)
    {
        value = value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value.ToLowerInvariant();
    }
}

public sealed class ExportService(AppDbContext db, ICryptoService crypto, IExportSanitizer sanitizer, ISystemClock clock, IAuditService audit) : IExportService
{
    public async Task<ServiceResult<ExportFile>> ExportApplicationsAsync(Guid? actorUserId, ExportRequest request, CancellationToken cancellationToken)
    {
        var applications = ApplicationQueryFilter.Apply(db.Applications.AsNoTracking(), db, crypto, request.Filters);

        var rows = await applications.OrderByDescending(x => x.CreatedAt).Take(10_000).ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        var fileName = $"basvuru-export-{now:yyyyMMddHHmmss}.{request.Format.ToString().ToLowerInvariant()}";
        byte[] content;
        string contentType;
        if (request.Format == ExportFormat.Csv)
        {
            content = BuildCsv(rows);
            contentType = "text/csv; charset=utf-8";
        }
        else
        {
            content = BuildXlsx(rows);
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        }

        db.ExportLogs.Add(new ExportLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            FiltersJson = JsonSerializer.Serialize(request.Filters),
            Format = request.Format,
            Status = ExportStatus.Completed,
            RecordCount = rows.Count,
            FileName = fileName,
            CreatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(actorUserId, AuditActions.ExportCreated, nameof(ExportLog), null, new { request.Format, rows.Count }, cancellationToken);

        return ServiceResult<ExportFile>.Ok(new ExportFile(content, contentType, fileName));
    }

    private byte[] BuildCsv(IEnumerable<ApplicationRecord> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ReferenceNumber,FirstName,LastName,NationalId,Phone,Email,Status,CreatedAt");
        foreach (var row in rows)
        {
            var values = new[]
            {
                row.ReferenceNumber,
                row.FirstName,
                row.LastName,
                crypto.Decrypt(row.NationalIdEncrypted),
                crypto.Decrypt(row.PhoneEncrypted),
                crypto.Decrypt(row.EmailEncrypted),
                row.Status.ToString(),
                row.CreatedAt.ToString("O")
            }.Select(x => EscapeCsv(sanitizer.SanitizeCell(x)));
            builder.AppendLine(string.Join(",", values));
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    private byte[] BuildXlsx(IEnumerable<ApplicationRecord> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Başvurular");
        var headers = new[] { "ReferenceNumber", "FirstName", "LastName", "NationalId", "Phone", "Email", "Status", "CreatedAt" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var rowIndex = 2;
        foreach (var row in rows)
        {
            var values = new[]
            {
                row.ReferenceNumber,
                row.FirstName,
                row.LastName,
                crypto.Decrypt(row.NationalIdEncrypted),
                crypto.Decrypt(row.PhoneEncrypted),
                crypto.Decrypt(row.EmailEncrypted),
                row.Status.ToString(),
                row.CreatedAt.ToString("O")
            };
            for (var i = 0; i < values.Length; i++)
            {
                sheet.Cell(rowIndex, i + 1).Value = sanitizer.SanitizeCell(values[i]);
            }
            rowIndex++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string EscapeCsv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
