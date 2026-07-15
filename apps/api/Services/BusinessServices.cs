using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BasvuruAkis.Api.Data;
using BasvuruAkis.Api.Domain;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

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

public sealed class SmsProvider(IConfiguration configuration, IWebHostEnvironment environment, ILogger<SmsProvider> logger) : ISmsProvider
{
    public Task SendOtpAsync(string normalizedPhone, string code, CancellationToken cancellationToken)
    {
        if (!environment.IsProduction())
        {
            logger.LogInformation("Development SMS OTP generated for phone hash target. Code is available only in API response for non-production.");
            return Task.CompletedTask;
        }

        var provider = configuration["Sms:Provider"];
        var apiKey = configuration["Sms:ApiKey"];
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Production SMS provider configuration is required.");
        }

        logger.LogInformation("SMS provider adapter called for provider {Provider}.", provider);
        return Task.CompletedTask;
    }
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
        await smsProvider.SendOtpAsync(phone, code, cancellationToken);

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

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.ApplicationAssignments.Add(new ApplicationAssignment
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            RepresentativeOfficeId = representativeOfficeId,
            IsAutomatic = false,
            ActorUserId = actorUserId,
            Reason = reason,
            CreatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(actorUserId, AuditActions.ApplicationAssigned, nameof(ApplicationRecord), applicationId.ToString(), new { representativeOfficeId, reason }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult.Ok();
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

public sealed class ExportService(AppDbContext db, ICryptoService crypto, IExportSanitizer sanitizer, ISystemClock clock, IAuditService audit) : IExportService
{
    public async Task<ServiceResult<ExportFile>> ExportApplicationsAsync(Guid? actorUserId, ExportRequest request, CancellationToken cancellationToken)
    {
        var applications = db.Applications.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Filters.Status) && Enum.TryParse<ApplicationStatus>(request.Filters.Status, true, out var status))
        {
            applications = applications.Where(x => x.Status == status);
        }
        if (request.Filters.ProvinceId is not null)
        {
            applications = applications.Where(x => x.ProvinceId == request.Filters.ProvinceId);
        }

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
