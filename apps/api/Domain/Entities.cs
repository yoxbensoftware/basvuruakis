namespace BasvuruAkis.Api.Domain;

public enum PublishStatus
{
    Draft,
    Scheduled,
    Published,
    Archived
}

public enum LegalTextType
{
    PrivacyNotice,
    ExplicitConsent,
    CookiePolicy
}

public enum ApplicationStatus
{
    Submitted,
    Assigned,
    InReview,
    Completed,
    SoftDeleted,
    Anonymized
}

public enum AssignmentRuleScope
{
    Neighborhood,
    District,
    Province,
    Region,
    Default
}

public enum ExportFormat
{
    Csv,
    Xlsx
}

public enum ExportStatus
{
    Queued,
    Completed,
    Failed
}

public sealed class AdminUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool MfaEnabled { get; set; }
    public string? MfaSecret { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockoutUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<AdminUserPermission> Permissions { get; set; } = [];
}

public sealed class AdminUserPermission
{
    public Guid AdminUserId { get; set; }
    public AdminUser? AdminUser { get; set; }
    public string Permission { get; set; } = "";
}

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Region
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class Province
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? RegionId { get; set; }
}

public sealed class District
{
    public int Id { get; set; }
    public int ProvinceId { get; set; }
    public string Name { get; set; } = "";
}

public sealed class Neighborhood
{
    public int Id { get; set; }
    public int DistrictId { get; set; }
    public string Name { get; set; } = "";
}

public sealed class RepresentativeOffice
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AssignmentRule
{
    public int Id { get; set; }
    public AssignmentRuleScope Scope { get; set; }
    public int? ScopeId { get; set; }
    public int RepresentativeOfficeId { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ContentPage
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Body { get; set; } = "";
    public string SeoTitle { get; set; } = "";
    public string SeoDescription { get; set; } = "";
    public PublishStatus Status { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class LegalText
{
    public Guid Id { get; set; }
    public LegalTextType Type { get; set; }
    public string Version { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}

public sealed class OtpRequest
{
    public Guid Id { get; set; }
    public string PhoneHash { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset ResendAvailableAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public string? VerificationTokenHash { get; set; }
    public DateTimeOffset? VerificationTokenExpiresAt { get; set; }
    public DateTimeOffset? VerificationTokenUsedAt { get; set; }
    public string IpAddress { get; set; } = "";
    public string DeviceId { get; set; } = "";
}

public sealed class ApplicationRecord
{
    public Guid Id { get; set; }
    public string ReferenceNumber { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string NationalIdEncrypted { get; set; } = "";
    public string NationalIdHash { get; set; } = "";
    public string PhoneEncrypted { get; set; } = "";
    public string PhoneHash { get; set; } = "";
    public string EmailEncrypted { get; set; } = "";
    public string EmailHash { get; set; } = "";
    public string AddressEncrypted { get; set; } = "";
    public int ProvinceId { get; set; }
    public int DistrictId { get; set; }
    public int NeighborhoodId { get; set; }
    public string? PostalCode { get; set; }
    public bool IsPhoneVerified { get; set; }
    public ApplicationStatus Status { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AnonymizedAt { get; set; }
}

public sealed class ApplicationConsent
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public LegalTextType LegalTextType { get; set; }
    public string LegalTextVersion { get; set; } = "";
    public DateTimeOffset AcceptedAt { get; set; }
    public string IpAddress { get; set; } = "";
    public string UserAgent { get; set; } = "";

    public static ApplicationConsent For(Guid applicationId, LegalText text, string ipAddress, string userAgent, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        ApplicationId = applicationId,
        LegalTextType = text.Type,
        LegalTextVersion = text.Version,
        AcceptedAt = now,
        IpAddress = ipAddress,
        UserAgent = userAgent
    };
}

public sealed class ApplicationAssignment
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public int RepresentativeOfficeId { get; set; }
    public int? AssignmentRuleId { get; set; }
    public bool IsAutomatic { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ApplicationStatusHistory
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public ApplicationStatus FromStatus { get; set; }
    public ApplicationStatus ToStatus { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string MetadataJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class SecurityLog
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = "";
    public Guid? ActorUserId { get; set; }
    public string IpAddress { get; set; } = "";
    public string UserAgent { get; set; } = "";
    public string MetadataJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ExportLog
{
    public Guid Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public string FiltersJson { get; set; } = "";
    public int RecordCount { get; set; }
    public ExportFormat Format { get; set; }
    public ExportStatus Status { get; set; }
    public string? FileName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
