using System.Security.Claims;
using BasvuruAkis.Api;
using BasvuruAkis.Api.Data;
using BasvuruAkis.Api.Domain;
using BasvuruAkis.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
const int ReferenceNumberMaxAttempts = 10;

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:3000", "http://localhost:8080"];
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

var databaseProvider = builder.Configuration["Database:Provider"]
    ?? (builder.Environment.IsProduction() ? "Postgres" : "Sqlite");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
    {
        var connectionString = builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required for PostgreSQL.");
        options.UseNpgsql(PostgresConnectionStrings.Normalize(connectionString));
    }
    else
    {
        var connectionString = builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=basvuruakis-dev.db";
        options.UseSqlite(connectionString);
    }
});

builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddSingleton<IDataProtectionKeyProvider, ConfigurationDataProtectionKeyProvider>();
builder.Services.AddSingleton<IReferenceNumberGenerator, ReferenceNumberGenerator>();
builder.Services.AddSingleton<ICryptoService, CryptoService>();
builder.Services.AddSingleton<IMaskingService, MaskingService>();
builder.Services.AddSingleton<ITcknValidator, TcknValidator>();
builder.Services.AddSingleton<IExportSanitizer, ExportSanitizer>();
builder.Services.AddSingleton<ITotpService, TotpService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ISecurityLogService, SecurityLogService>();
builder.Services.AddScoped<ICaptchaVerifier, CaptchaVerifier>();
builder.Services.AddScoped<ISmsProvider, SmsProvider>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IAssignmentService, AssignmentService>();
builder.Services.AddScoped<IAnonymizationService, AnonymizationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IExportService, ExportService>();

var signingKey = TokenService.ResolveSigningKey(builder.Configuration, builder.Environment);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = TokenService.Issuer,
            ValidateAudience = true,
            ValidAudience = TokenService.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseSecurityHeaders();
app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!app.Environment.IsProduction())
    {
        await db.Database.EnsureCreatedAsync();
        await DemoSeed.SeedAsync(scope.ServiceProvider);
    }
    else
    {
        await ProductionBootstrap.EnsureAdminAsync(scope.ServiceProvider);
    }
}

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (AppDbContext db, IWebHostEnvironment environment, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    if (!canConnect)
    {
        return Results.Problem("Database is not reachable", statusCode: 503);
    }

    if (environment.IsProduction() && !await OperationalDataReadyAsync(db, cancellationToken))
    {
        return Results.Problem("Required operational data is missing", statusCode: 503);
    }

    return Results.Ok(new { status = "ready" });
});

var publicApi = app.MapGroup("/api");

publicApi.MapGet("/content/pages/{slug}", async (string slug, AppDbContext db) =>
{
    var page = await db.ContentPages.AsNoTracking()
        .Where(x => x.Slug == slug && x.Status == PublishStatus.Published)
        .Select(x => new PublicContentPageResponse(x.Slug, x.Title, x.Summary, x.Body, x.SeoTitle, x.SeoDescription))
        .FirstOrDefaultAsync();

    return page is null ? Results.NotFound() : Results.Ok(page);
});

publicApi.MapGet("/legal-texts/active", async (AppDbContext db) =>
{
    var texts = await db.LegalTexts.AsNoTracking()
        .Where(x => x.IsActive)
        .OrderBy(x => x.Type)
        .Select(x => new LegalTextResponse(x.Id, x.Type.ToString(), x.Version, x.Title, x.Body, x.PublishedAt))
        .ToListAsync();

    return Results.Ok(texts);
});

publicApi.MapPost("/otp/request", async Task<Results<Ok<OtpRequestResponse>, BadRequest<ApiError>>> (
    OtpRequestDto request,
    HttpContext httpContext,
    IOtpService otpService,
    CancellationToken cancellationToken) =>
{
    var context = new OtpRequestContext(httpContext.GetClientIp(), httpContext.Request.Headers.UserAgent.ToString());
    var result = await otpService.RequestAsync(request, context, cancellationToken);
    return result.Success
        ? TypedResults.Ok(result.Value!)
        : TypedResults.BadRequest(new ApiError(result.ErrorCode, result.Message));
});

publicApi.MapPost("/otp/verify", async Task<Results<Ok<OtpVerifyResponse>, BadRequest<ApiError>>> (
    OtpVerifyDto request,
    HttpContext httpContext,
    IOtpService otpService,
    CancellationToken cancellationToken) =>
{
    var context = new OtpRequestContext(httpContext.GetClientIp(), httpContext.Request.Headers.UserAgent.ToString());
    var result = await otpService.VerifyAsync(request, context, cancellationToken);
    return result.Success
        ? TypedResults.Ok(result.Value!)
        : TypedResults.BadRequest(new ApiError(result.ErrorCode, result.Message));
});

publicApi.MapPost("/applications", async Task<Results<Created<ApplicationCreatedResponse>, Ok<ApplicationCreatedResponse>, BadRequest<ApiError>, Conflict<ApiError>, ProblemHttpResult>> (
    CreateApplicationRequest request,
    HttpContext httpContext,
    AppDbContext db,
    ICryptoService crypto,
    ITcknValidator tcknValidator,
    IOtpService otpService,
    IAssignmentService assignmentService,
    IReferenceNumberGenerator referenceNumberGenerator,
    ISystemClock clock,
    CancellationToken cancellationToken) =>
{
    var validationError = ValidateApplicationRequest(request, tcknValidator);
    if (validationError is not null)
    {
        return TypedResults.BadRequest(validationError);
    }

    var existingByIdempotency = await db.Applications
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
    if (existingByIdempotency is not null)
    {
        return TypedResults.Ok(new ApplicationCreatedResponse(existingByIdempotency.Id, existingByIdempotency.ReferenceNumber, existingByIdempotency.Status.ToString()));
    }

    var normalizedNationalId = Normalization.NormalizeDigits(request.NationalId);
    var normalizedPhone = Normalization.NormalizePhone(request.Phone);
    var normalizedEmail = Normalization.NormalizeEmail(request.Email);
    var nationalIdHash = crypto.HashLookup(normalizedNationalId);
    var phoneHash = crypto.HashLookup(normalizedPhone);
    var emailHash = crypto.HashLookup(normalizedEmail);

    var duplicateExists = await db.Applications.AsNoTracking()
        .AnyAsync(x => x.NationalIdHash == nationalIdHash || x.PhoneHash == phoneHash, cancellationToken);
    if (duplicateExists)
    {
        return TypedResults.Conflict(new ApiError("duplicate_application", "Başvuru alınamadı. Bilgileri kontrol edip daha sonra tekrar deneyin."));
    }

    var legalTexts = await db.LegalTexts.Where(x => x.IsActive).ToListAsync(cancellationToken);
    var privacy = legalTexts.FirstOrDefault(x => x.Type == LegalTextType.PrivacyNotice);
    var consent = legalTexts.FirstOrDefault(x => x.Type == LegalTextType.ExplicitConsent);
    if (privacy is null || consent is null)
    {
        return TypedResults.BadRequest(new ApiError("legal_text_missing", "Aktif KVKK metinleri tanımlı değil."));
    }

    if (!await LocationExistsAsync(db, request.ProvinceId, request.DistrictId, request.NeighborhoodId, cancellationToken))
    {
        return TypedResults.BadRequest(new ApiError("location_not_found", "Seçilen il, ilçe veya mahalle geçerli değil."));
    }

    if (!await db.RepresentativeOffices.AnyAsync(x => x.IsDefault && x.IsActive, cancellationToken))
    {
        return TypedResults.BadRequest(new ApiError("assignment_not_configured", "Başvuru yönlendirme yapılandırması eksik."));
    }

    var now = clock.UtcNow;
    var referenceNumber = await GenerateUnusedReferenceNumberAsync(db, referenceNumberGenerator, now, cancellationToken);
    if (referenceNumber is null)
    {
        return TypedResults.Problem("Başvuru referans numarası üretilemedi.", statusCode: 503);
    }

    var tokenConsumed = await otpService.ConsumeVerificationTokenAsync(normalizedPhone, request.VerificationToken, cancellationToken);
    if (!tokenConsumed)
    {
        return TypedResults.BadRequest(new ApiError("phone_not_verified", "Telefon doğrulaması tamamlanmadan başvuru oluşturulamaz."));
    }

    var nextReferenceNumber = referenceNumber;
    for (var attempt = 0; attempt < ReferenceNumberMaxAttempts; attempt++)
    {
        if (attempt > 0)
        {
            nextReferenceNumber = await GenerateUnusedReferenceNumberAsync(db, referenceNumberGenerator, now, cancellationToken);
            if (nextReferenceNumber is null)
            {
                return TypedResults.Problem("Başvuru referans numarası üretilemedi.", statusCode: 503);
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var application = new ApplicationRecord
        {
            Id = Guid.NewGuid(),
            ReferenceNumber = nextReferenceNumber,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            NationalIdEncrypted = crypto.Encrypt(normalizedNationalId),
            NationalIdHash = nationalIdHash,
            PhoneEncrypted = crypto.Encrypt(normalizedPhone),
            PhoneHash = phoneHash,
            EmailEncrypted = crypto.Encrypt(normalizedEmail),
            EmailHash = emailHash,
            AddressEncrypted = crypto.Encrypt(request.Address.Trim()),
            ProvinceId = request.ProvinceId,
            DistrictId = request.DistrictId,
            NeighborhoodId = request.NeighborhoodId,
            PostalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? null : request.PostalCode.Trim(),
            Status = ApplicationStatus.Submitted,
            IsPhoneVerified = true,
            IdempotencyKey = request.IdempotencyKey,
            CreatedAt = now
        };
        db.Applications.Add(application);
        db.ApplicationConsents.AddRange(
            ApplicationConsent.For(application.Id, privacy, httpContext.GetClientIp(), httpContext.Request.Headers.UserAgent.ToString(), now),
            ApplicationConsent.For(application.Id, consent, httpContext.GetClientIp(), httpContext.Request.Headers.UserAgent.ToString(), now));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await assignmentService.AssignAutomaticallyAsync(application.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var response = new ApplicationCreatedResponse(application.Id, application.ReferenceNumber, application.Status.ToString());
            return TypedResults.Created($"/api/applications/{application.Id}", response);
        }
        catch (DbUpdateException error) when (IsApplicationReferenceNumberConstraint(error))
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            if (attempt + 1 >= ReferenceNumberMaxAttempts)
            {
                return TypedResults.Problem("Başvuru referans numarası üretilemedi.", statusCode: 503);
            }
        }
        catch (DbUpdateException error) when (IsApplicationIdempotencyConstraint(error))
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var existing = await db.Applications.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return TypedResults.Ok(new ApplicationCreatedResponse(existing.Id, existing.ReferenceNumber, existing.Status.ToString()));
            }

            return TypedResults.Conflict(new ApiError("duplicate_application", "Başvuru alınamadı. Bilgileri kontrol edip daha sonra tekrar deneyin."));
        }
        catch (DbUpdateException error) when (IsApplicationDuplicateConstraint(error))
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return TypedResults.Conflict(new ApiError("duplicate_application", "Başvuru alınamadı. Bilgileri kontrol edip daha sonra tekrar deneyin."));
        }
    }

    return TypedResults.Problem("Başvuru referans numarası üretilemedi.", statusCode: 503);
});

var admin = app.MapGroup("/api/admin");

admin.MapPost("/auth/login", async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, BadRequest<ApiError>>> (
    AdminLoginRequest request,
    IAuthService authService,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await authService.LoginAsync(request, httpContext.GetClientIp(), httpContext.Request.Headers.UserAgent.ToString(), cancellationToken);
    if (result.Success)
    {
        return TypedResults.Ok(result.Value!);
    }

    if (result.ErrorCode == "mfa_required")
    {
        return TypedResults.BadRequest(new ApiError(result.ErrorCode, result.Message));
    }

    return TypedResults.Unauthorized();
});

admin.MapPost("/auth/refresh", async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> (
    RefreshTokenRequest request,
    IAuthService authService,
    CancellationToken cancellationToken) =>
{
    var result = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
    return result.Success ? TypedResults.Ok(result.Value!) : TypedResults.Unauthorized();
});

admin.MapPost("/auth/logout", async Task<Results<NoContent, UnauthorizedHttpResult>> (
    RefreshTokenRequest request,
    ClaimsPrincipal user,
    IAuthService authService,
    CancellationToken cancellationToken) =>
{
    var userId = user.GetUserId();
    if (userId is null)
    {
        return TypedResults.Unauthorized();
    }

    await authService.LogoutAsync(userId.Value, request.RefreshToken, cancellationToken);
    return TypedResults.NoContent();
}).RequireAuthorization();

admin.MapGet("/applications", async Task<Results<Ok<PagedResult<ApplicationListItem>>, ForbidHttpResult>> (
    HttpContext httpContext,
    AppDbContext db,
    IMaskingService masking,
    ICryptoService crypto,
    [AsParameters] ApplicationQuery query,
    CancellationToken cancellationToken) =>
{
    if (!httpContext.User.HasPermission(Permissions.ApplicationsRead))
    {
        return TypedResults.Forbid();
    }

    var page = Math.Clamp(query.Page ?? 1, 1, 10000);
    var pageSize = Math.Clamp(query.PageSize ?? 20, 1, 100);
    var applications = ApplicationQueryFilter.Apply(db.Applications.AsNoTracking(), db, crypto, query);

    var desc = query.Desc == true;
    applications = query.Sort?.ToLowerInvariant() switch
    {
        "name" => desc ? applications.OrderByDescending(x => x.LastName).ThenByDescending(x => x.FirstName) : applications.OrderBy(x => x.LastName).ThenBy(x => x.FirstName),
        "status" => desc ? applications.OrderByDescending(x => x.Status) : applications.OrderBy(x => x.Status),
        _ => desc ? applications.OrderByDescending(x => x.CreatedAt) : applications.OrderBy(x => x.CreatedAt)
    };

    var total = await applications.CountAsync(cancellationToken);
    var items = await applications
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(x => new ApplicationListItem(
            x.Id,
            x.ReferenceNumber,
            masking.MaskName(x.FirstName, x.LastName),
            masking.MaskHashOnly(x.NationalIdHash),
            masking.MaskHashOnly(x.PhoneHash),
            x.ProvinceId,
            x.DistrictId,
            x.NeighborhoodId,
            x.Status.ToString(),
            x.CreatedAt))
        .ToListAsync(cancellationToken);

    return TypedResults.Ok(new PagedResult<ApplicationListItem>(items, page, pageSize, total));
}).RequireAuthorization();

admin.MapGet("/applications/{id:guid}", async Task<Results<Ok<ApplicationDetailResponse>, NotFound, ForbidHttpResult>> (
    Guid id,
    HttpContext httpContext,
    AppDbContext db,
    ICryptoService crypto,
    IAuditService audit,
    CancellationToken cancellationToken) =>
{
    if (!httpContext.User.HasPermission(Permissions.ApplicationsDetailRead))
    {
        return TypedResults.Forbid();
    }

    var application = await db.Applications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (application is null)
    {
        return TypedResults.NotFound();
    }

    await audit.WriteAsync(httpContext.User.GetUserId(), AuditActions.ApplicationViewed, nameof(ApplicationRecord), id.ToString(), new { application.ReferenceNumber }, cancellationToken);

    var detail = new ApplicationDetailResponse(
        application.Id,
        application.ReferenceNumber,
        application.FirstName,
        application.LastName,
        crypto.Decrypt(application.NationalIdEncrypted),
        crypto.Decrypt(application.PhoneEncrypted),
        crypto.Decrypt(application.EmailEncrypted),
        crypto.Decrypt(application.AddressEncrypted),
        application.ProvinceId,
        application.DistrictId,
        application.NeighborhoodId,
        application.PostalCode,
        application.Status.ToString(),
        application.CreatedAt);

    return TypedResults.Ok(detail);
}).RequireAuthorization();

admin.MapPost("/applications/{id:guid}/assignment", async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<ApiError>>> (
    Guid id,
    ManualAssignmentRequest request,
    HttpContext httpContext,
    IAssignmentService assignmentService,
    CancellationToken cancellationToken) =>
{
    if (!httpContext.User.HasPermission(Permissions.ApplicationsAssign))
    {
        return TypedResults.Forbid();
    }

    var userId = httpContext.User.GetUserId();
    if (userId is null)
    {
        return TypedResults.Forbid();
    }

    var result = await assignmentService.AssignManuallyAsync(id, request.RepresentativeOfficeId, userId.Value, request.Reason, cancellationToken);
    return result.Success
        ? TypedResults.NoContent()
        : result.ErrorCode == "not_found" ? TypedResults.NotFound() : TypedResults.BadRequest(new ApiError(result.ErrorCode, result.Message));
}).RequireAuthorization();

admin.MapPost("/applications/{id:guid}/anonymize", async Task<Results<Ok<ApplicationAnonymizedResponse>, NotFound, ForbidHttpResult, BadRequest<ApiError>>> (
    Guid id,
    AnonymizeApplicationRequest request,
    HttpContext httpContext,
    IAnonymizationService anonymizationService,
    CancellationToken cancellationToken) =>
{
    if (!httpContext.User.HasPermission(Permissions.ApplicationsAnonymize))
    {
        return TypedResults.Forbid();
    }

    var userId = httpContext.User.GetUserId();
    if (userId is null)
    {
        return TypedResults.Forbid();
    }

    var result = await anonymizationService.AnonymizeApplicationAsync(id, userId.Value, request.Reason, cancellationToken);
    if (result.Success)
    {
        return TypedResults.Ok(result.Value!);
    }

    return result.ErrorCode == "not_found"
        ? TypedResults.NotFound()
        : TypedResults.BadRequest(new ApiError(result.ErrorCode, result.Message));
}).RequireAuthorization();

admin.MapGet("/dashboard", async Task<Results<Ok<DashboardResponse>, ForbidHttpResult>> (
    HttpContext httpContext,
    AppDbContext db,
    ISystemClock clock,
    CancellationToken cancellationToken) =>
{
    if (!httpContext.User.HasPermission(Permissions.DashboardRead))
    {
        return TypedResults.Forbid();
    }

    var now = clock.UtcNow;
    var today = now.Date;
    var last7 = now.AddDays(-7);
    var last30 = now.AddDays(-30);

    var total = await db.Applications.CountAsync(cancellationToken);
    var todayCount = await db.Applications.CountAsync(x => x.CreatedAt >= today, cancellationToken);
    var last7Count = await db.Applications.CountAsync(x => x.CreatedAt >= last7, cancellationToken);
    var last30Count = await db.Applications.CountAsync(x => x.CreatedAt >= last30, cancellationToken);
    var unassigned = await db.Applications.CountAsync(x => !db.ApplicationAssignments.Any(a => a.ApplicationId == x.Id), cancellationToken);
    var verified = await db.Applications.CountAsync(x => x.IsPhoneVerified, cancellationToken);

    var byProvince = await db.Applications
        .GroupBy(x => x.ProvinceId)
        .Select(x => new DistributionItem(x.Key.ToString(), x.Count()))
        .ToListAsync(cancellationToken);

    return TypedResults.Ok(new DashboardResponse(total, todayCount, last7Count, last30Count, verified, total - verified, unassigned, byProvince));
}).RequireAuthorization();

admin.MapGet("/audit-logs", async Task<Results<Ok<PagedResult<AuditLogItem>>, ForbidHttpResult>> (
    HttpContext httpContext,
    AppDbContext db,
    [AsParameters] AuditLogQuery query,
    CancellationToken cancellationToken) =>
{
    if (!httpContext.User.HasPermission(Permissions.AuditRead))
    {
        return TypedResults.Forbid();
    }

    var page = Math.Clamp(query.Page ?? 1, 1, 10000);
    var pageSize = Math.Clamp(query.PageSize ?? 20, 1, 100);
    var logs = db.AuditLogs.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(query.Action))
    {
        logs = logs.Where(x => x.Action == query.Action);
    }

    var total = await logs.CountAsync(cancellationToken);
    var items = await logs
        .OrderByDescending(x => x.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(x => new AuditLogItem(x.Id, x.ActorUserId, x.Action, x.EntityType, x.EntityId, x.MetadataJson, x.CreatedAt))
        .ToListAsync(cancellationToken);

    return TypedResults.Ok(new PagedResult<AuditLogItem>(items, page, pageSize, total));
}).RequireAuthorization();

admin.MapGet("/security-logs", async Task<Results<Ok<PagedResult<SecurityLogItem>>, ForbidHttpResult>> (
    HttpContext httpContext,
    AppDbContext db,
    [AsParameters] SecurityLogQuery query,
    CancellationToken cancellationToken) =>
{
    if (!httpContext.User.HasPermission(Permissions.AuditRead))
    {
        return TypedResults.Forbid();
    }

    var page = Math.Clamp(query.Page ?? 1, 1, 10000);
    var pageSize = Math.Clamp(query.PageSize ?? 20, 1, 100);
    var logs = db.SecurityLogs.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(query.EventType))
    {
        logs = logs.Where(x => x.EventType == query.EventType);
    }

    var total = await logs.CountAsync(cancellationToken);
    var items = await logs
        .OrderByDescending(x => x.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(x => new SecurityLogItem(x.Id, x.EventType, x.ActorUserId, x.IpAddress, x.UserAgent, x.MetadataJson, x.CreatedAt))
        .ToListAsync(cancellationToken);

    return TypedResults.Ok(new PagedResult<SecurityLogItem>(items, page, pageSize, total));
}).RequireAuthorization();

admin.MapPost("/exports", async Task<Results<FileContentHttpResult, ForbidHttpResult, BadRequest<ApiError>>> (
    ExportRequest request,
    HttpContext httpContext,
    IExportService exportService,
    CancellationToken cancellationToken) =>
{
    if (!httpContext.User.HasPermission(Permissions.ApplicationsExport))
    {
        return TypedResults.Forbid();
    }

    var result = await exportService.ExportApplicationsAsync(httpContext.User.GetUserId(), request, cancellationToken);
    if (!result.Success)
    {
        return TypedResults.BadRequest(new ApiError(result.ErrorCode, result.Message));
    }

    return TypedResults.File(result.Value!.Content, result.Value.ContentType, result.Value.FileName);
}).RequireAuthorization();

static ApiError? ValidateApplicationRequest(CreateApplicationRequest request, ITcknValidator tcknValidator)
{
    if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
    {
        return new ApiError("name_required", "Ad ve soyad zorunludur.");
    }
    if (!tcknValidator.IsValid(request.NationalId))
    {
        return new ApiError("invalid_national_id", "TCKN algoritmik olarak geçerli değil. Resmi kimlik doğrulaması yapılmaz.");
    }
    if (string.IsNullOrWhiteSpace(request.Phone) || Normalization.NormalizePhone(request.Phone).Length < 10)
    {
        return new ApiError("invalid_phone", "Telefon formatı geçerli değil.");
    }
    if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@', StringComparison.Ordinal))
    {
        return new ApiError("invalid_email", "E-posta formatı geçerli değil.");
    }
    if (request.ProvinceId <= 0 || request.DistrictId <= 0 || request.NeighborhoodId <= 0)
    {
        return new ApiError("location_required", "İl, ilçe ve mahalle zorunludur.");
    }
    if (string.IsNullOrWhiteSpace(request.Address))
    {
        return new ApiError("address_required", "Açık adres zorunludur.");
    }
    if (!request.PrivacyNoticeAccepted || !request.ExplicitConsentAccepted)
    {
        return new ApiError("consent_required", "KVKK aydınlatma ve açık rıza onayları zorunludur.");
    }
    if (string.IsNullOrWhiteSpace(request.VerificationToken))
    {
        return new ApiError("verification_required", "Telefon doğrulama tokenı zorunludur.");
    }
    if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length < 16)
    {
        return new ApiError("idempotency_key_required", "Geçerli bir idempotency key zorunludur.");
    }

    return null;
}

static async Task<bool> LocationExistsAsync(AppDbContext db, int provinceId, int districtId, int neighborhoodId, CancellationToken cancellationToken) =>
    await db.Neighborhoods.AsNoTracking()
        .AnyAsync(neighborhood =>
            neighborhood.Id == neighborhoodId &&
            neighborhood.DistrictId == districtId &&
            db.Districts.Any(district => district.Id == districtId && district.ProvinceId == provinceId),
            cancellationToken);

static async Task<string?> GenerateUnusedReferenceNumberAsync(
    AppDbContext db,
    IReferenceNumberGenerator referenceNumberGenerator,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    for (var attempt = 0; attempt < ReferenceNumberMaxAttempts; attempt++)
    {
        var referenceNumber = referenceNumberGenerator.Generate(now);
        var exists = await db.Applications.AsNoTracking()
            .AnyAsync(application => application.ReferenceNumber == referenceNumber, cancellationToken);
        if (!exists)
        {
            return referenceNumber;
        }
    }

    return null;
}

static bool IsApplicationDuplicateConstraint(DbUpdateException error) =>
    IsUniqueConstraintViolation(
        error,
        "IX_Applications_NationalIdHash",
        "IX_Applications_PhoneHash",
        "Applications.NationalIdHash",
        "Applications.PhoneHash");

static bool IsApplicationIdempotencyConstraint(DbUpdateException error) =>
    IsUniqueConstraintViolation(
        error,
        "IX_Applications_IdempotencyKey",
        "Applications.IdempotencyKey");

static bool IsApplicationReferenceNumberConstraint(DbUpdateException error) =>
    IsUniqueConstraintViolation(
        error,
        "IX_Applications_ReferenceNumber",
        "Applications.ReferenceNumber");

static bool IsUniqueConstraintViolation(DbUpdateException error, params string[] identifiers)
{
    if (error.InnerException is PostgresException { SqlState: "23505" } postgres)
    {
        return identifiers.Any(identifier =>
            string.Equals(postgres.ConstraintName, identifier, StringComparison.OrdinalIgnoreCase) ||
            postgres.MessageText.Contains(identifier, StringComparison.OrdinalIgnoreCase));
    }

    if (error.InnerException is SqliteException { SqliteErrorCode: 19 } sqlite)
    {
        return identifiers.Any(identifier => sqlite.Message.Contains(identifier, StringComparison.OrdinalIgnoreCase));
    }

    var message = error.InnerException?.Message ?? error.Message;
    return identifiers.Any(identifier => message.Contains(identifier, StringComparison.OrdinalIgnoreCase));
}

static async Task<bool> OperationalDataReadyAsync(AppDbContext db, CancellationToken cancellationToken)
{
    var hasPrivacyNotice = await db.LegalTexts.AsNoTracking()
        .AnyAsync(text => text.IsActive && text.Type == LegalTextType.PrivacyNotice, cancellationToken);
    if (!hasPrivacyNotice)
    {
        return false;
    }

    var hasExplicitConsent = await db.LegalTexts.AsNoTracking()
        .AnyAsync(text => text.IsActive && text.Type == LegalTextType.ExplicitConsent, cancellationToken);
    if (!hasExplicitConsent)
    {
        return false;
    }

    var hasDefaultOffice = await db.RepresentativeOffices.AsNoTracking()
        .AnyAsync(office => office.IsDefault && office.IsActive, cancellationToken);
    if (!hasDefaultOffice)
    {
        return false;
    }

    return await db.Neighborhoods.AsNoTracking().AnyAsync(cancellationToken);
}

app.Run();

public partial class Program;
