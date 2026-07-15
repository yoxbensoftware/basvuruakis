using BasvuruAkis.Api.Domain;

namespace BasvuruAkis.Api;

public sealed record ApiError(string Code, string Message);

public sealed record OtpRequestDto(string Phone, string CaptchaToken, string? DeviceId);
public sealed record OtpRequestResponse(Guid RequestId, DateTimeOffset ExpiresAt, DateTimeOffset ResendAvailableAt, string? DevelopmentCode);
public sealed record OtpVerifyDto(string Phone, string Code, string? DeviceId);
public sealed record OtpVerifyResponse(string VerificationToken, DateTimeOffset ExpiresAt);
public sealed record OtpRequestContext(string IpAddress, string UserAgent);

public sealed record CreateApplicationRequest(
    string FirstName,
    string LastName,
    string NationalId,
    string Phone,
    string Email,
    int ProvinceId,
    int DistrictId,
    int NeighborhoodId,
    string Address,
    string? PostalCode,
    bool PrivacyNoticeAccepted,
    bool ExplicitConsentAccepted,
    string VerificationToken,
    string IdempotencyKey);

public sealed record ApplicationCreatedResponse(Guid Id, string ReferenceNumber, string Status);

public sealed record PublicContentPageResponse(string Slug, string Title, string Summary, string Body, string SeoTitle, string SeoDescription);
public sealed record LegalTextResponse(Guid Id, string Type, string Version, string Title, string Body, DateTimeOffset PublishedAt);

public sealed record AdminLoginRequest(string Email, string Password, string? TotpCode);
public sealed record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt, DateTimeOffset RefreshTokenExpiresAt, string[] Permissions);
public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record ApplicationQuery(
    int? Page,
    int? PageSize,
    string? Sort,
    bool? Desc,
    string? Status,
    int? ProvinceId,
    int? DistrictId,
    int? NeighborhoodId,
    DateTimeOffset? From,
    DateTimeOffset? To);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record ApplicationListItem(
    Guid Id,
    string ReferenceNumber,
    string FullNameMasked,
    string NationalIdMasked,
    string PhoneMasked,
    int ProvinceId,
    int DistrictId,
    int NeighborhoodId,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record ApplicationDetailResponse(
    Guid Id,
    string ReferenceNumber,
    string FirstName,
    string LastName,
    string NationalId,
    string Phone,
    string Email,
    string Address,
    int ProvinceId,
    int DistrictId,
    int NeighborhoodId,
    string? PostalCode,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record ManualAssignmentRequest(int RepresentativeOfficeId, string Reason);
public sealed record AnonymizeApplicationRequest(string Reason);
public sealed record ApplicationAnonymizedResponse(Guid Id, string Status, DateTimeOffset AnonymizedAt);

public sealed record DashboardResponse(
    int Total,
    int Today,
    int Last7Days,
    int Last30Days,
    int Verified,
    int Unverified,
    int Unassigned,
    IReadOnlyList<DistributionItem> ByProvince);

public sealed record DistributionItem(string Label, int Count);

public sealed record ExportRequest(ExportFormat Format, ApplicationQuery Filters);
public sealed record ExportFile(byte[] Content, string ContentType, string FileName);
