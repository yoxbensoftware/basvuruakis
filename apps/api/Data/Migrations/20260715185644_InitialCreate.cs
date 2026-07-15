using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BasvuruAkis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    MfaEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MfaSecret = table.Column<string>(type: "text", nullable: true),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false),
                    LockoutUntil = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepresentativeOfficeId = table.Column<int>(type: "integer", nullable: false),
                    AssignmentRuleId = table.Column<int>(type: "integer", nullable: true),
                    IsAutomatic = table.Column<bool>(type: "boolean", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalTextType = table.Column<int>(type: "integer", nullable: false),
                    LegalTextVersion = table.Column<string>(type: "text", nullable: false),
                    AcceptedAt = table.Column<long>(type: "bigint", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: false),
                    UserAgent = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationConsents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    NationalIdEncrypted = table.Column<string>(type: "text", nullable: false),
                    NationalIdHash = table.Column<string>(type: "text", nullable: false),
                    PhoneEncrypted = table.Column<string>(type: "text", nullable: false),
                    PhoneHash = table.Column<string>(type: "text", nullable: false),
                    EmailEncrypted = table.Column<string>(type: "text", nullable: false),
                    EmailHash = table.Column<string>(type: "text", nullable: false),
                    AddressEncrypted = table.Column<string>(type: "text", nullable: false),
                    ProvinceId = table.Column<int>(type: "integer", nullable: false),
                    DistrictId = table.Column<int>(type: "integer", nullable: false),
                    NeighborhoodId = table.Column<int>(type: "integer", nullable: false),
                    PostalCode = table.Column<string>(type: "text", nullable: true),
                    IsPhoneVerified = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    AnonymizedAt = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationStatusHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScopeId = table.Column<int>(type: "integer", nullable: true),
                    RepresentativeOfficeId = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ValidFrom = table.Column<long>(type: "bigint", nullable: true),
                    ValidUntil = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    SeoTitle = table.Column<string>(type: "text", nullable: false),
                    SeoDescription = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PublishedAt = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentPages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Districts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProvinceId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Districts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExportLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FiltersJson = table.Column<string>(type: "text", nullable: false),
                    RecordCount = table.Column<int>(type: "integer", nullable: false),
                    Format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegalTexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalTexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Neighborhoods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DistrictId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Neighborhoods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OtpRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneHash = table.Column<string>(type: "text", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    ResendAvailableAt = table.Column<long>(type: "bigint", nullable: false),
                    VerifiedAt = table.Column<long>(type: "bigint", nullable: true),
                    VerificationTokenHash = table.Column<string>(type: "text", nullable: true),
                    VerificationTokenExpiresAt = table.Column<long>(type: "bigint", nullable: true),
                    VerificationTokenUsedAt = table.Column<long>(type: "bigint", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Provinces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    RegionId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    RevokedAt = table.Column<long>(type: "bigint", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepresentativeOffices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepresentativeOffices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: false),
                    UserAgent = table.Column<string>(type: "text", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminUserPermissions",
                columns: table => new
                {
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permission = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUserPermissions", x => new { x.AdminUserId, x.Permission });
                    table.ForeignKey(
                        name: "FK_AdminUserPermissions_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_Email",
                table: "AdminUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAssignments_ApplicationId_CreatedAt",
                table: "ApplicationAssignments",
                columns: new[] { "ApplicationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_EmailHash",
                table: "Applications",
                column: "EmailHash");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_IdempotencyKey",
                table: "Applications",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_NationalIdHash",
                table: "Applications",
                column: "NationalIdHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_PhoneHash",
                table: "Applications",
                column: "PhoneHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ProvinceId_DistrictId_NeighborhoodId_CreatedAt",
                table: "Applications",
                columns: new[] { "ProvinceId", "DistrictId", "NeighborhoodId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ReferenceNumber",
                table: "Applications",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRules_Scope_ScopeId_Priority_IsActive",
                table: "AssignmentRules",
                columns: new[] { "Scope", "ScopeId", "Priority", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentPages_Slug",
                table: "ContentPages",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Districts_ProvinceId_Name",
                table: "Districts",
                columns: new[] { "ProvinceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalTexts_Type_IsActive",
                table: "LegalTexts",
                columns: new[] { "Type", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalTexts_Type_Version",
                table: "LegalTexts",
                columns: new[] { "Type", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Neighborhoods_DistrictId_Name",
                table: "Neighborhoods",
                columns: new[] { "DistrictId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtpRequests_DeviceId_CreatedAt",
                table: "OtpRequests",
                columns: new[] { "DeviceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpRequests_IpAddress_CreatedAt",
                table: "OtpRequests",
                columns: new[] { "IpAddress", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpRequests_PhoneHash_CreatedAt",
                table: "OtpRequests",
                columns: new[] { "PhoneHash", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpRequests_VerificationTokenHash",
                table: "OtpRequests",
                column: "VerificationTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_Provinces_Name",
                table: "Provinces",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepresentativeOffices_Name",
                table: "RepresentativeOffices",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecurityLogs_EventType_CreatedAt",
                table: "SecurityLogs",
                columns: new[] { "EventType", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminUserPermissions");

            migrationBuilder.DropTable(
                name: "ApplicationAssignments");

            migrationBuilder.DropTable(
                name: "ApplicationConsents");

            migrationBuilder.DropTable(
                name: "Applications");

            migrationBuilder.DropTable(
                name: "ApplicationStatusHistories");

            migrationBuilder.DropTable(
                name: "AssignmentRules");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ContentPages");

            migrationBuilder.DropTable(
                name: "Districts");

            migrationBuilder.DropTable(
                name: "ExportLogs");

            migrationBuilder.DropTable(
                name: "LegalTexts");

            migrationBuilder.DropTable(
                name: "Neighborhoods");

            migrationBuilder.DropTable(
                name: "OtpRequests");

            migrationBuilder.DropTable(
                name: "Provinces");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Regions");

            migrationBuilder.DropTable(
                name: "RepresentativeOffices");

            migrationBuilder.DropTable(
                name: "SecurityLogs");

            migrationBuilder.DropTable(
                name: "AdminUsers");
        }
    }
}
