using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasvuruAkis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRelationalConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SecurityLogs_ActorUserId",
                table: "SecurityLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_AdminUserId",
                table: "RefreshTokens",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Provinces_RegionId",
                table: "Provinces",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportLogs_ActorUserId",
                table: "ExportLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRules_RepresentativeOfficeId",
                table: "AssignmentRules",
                column: "RepresentativeOfficeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistories_ActorUserId",
                table: "ApplicationStatusHistories",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistories_ApplicationId",
                table: "ApplicationStatusHistories",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_DistrictId",
                table: "Applications",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_NeighborhoodId",
                table: "Applications",
                column: "NeighborhoodId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationConsents_ApplicationId",
                table: "ApplicationConsents",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAssignments_ActorUserId",
                table: "ApplicationAssignments",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAssignments_AssignmentRuleId",
                table: "ApplicationAssignments",
                column: "AssignmentRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAssignments_RepresentativeOfficeId",
                table: "ApplicationAssignments",
                column: "RepresentativeOfficeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationAssignments_AdminUsers_ActorUserId",
                table: "ApplicationAssignments",
                column: "ActorUserId",
                principalTable: "AdminUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationAssignments_Applications_ApplicationId",
                table: "ApplicationAssignments",
                column: "ApplicationId",
                principalTable: "Applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationAssignments_AssignmentRules_AssignmentRuleId",
                table: "ApplicationAssignments",
                column: "AssignmentRuleId",
                principalTable: "AssignmentRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationAssignments_RepresentativeOffices_Representative~",
                table: "ApplicationAssignments",
                column: "RepresentativeOfficeId",
                principalTable: "RepresentativeOffices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationConsents_Applications_ApplicationId",
                table: "ApplicationConsents",
                column: "ApplicationId",
                principalTable: "Applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Districts_DistrictId",
                table: "Applications",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Neighborhoods_NeighborhoodId",
                table: "Applications",
                column: "NeighborhoodId",
                principalTable: "Neighborhoods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Provinces_ProvinceId",
                table: "Applications",
                column: "ProvinceId",
                principalTable: "Provinces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationStatusHistories_AdminUsers_ActorUserId",
                table: "ApplicationStatusHistories",
                column: "ActorUserId",
                principalTable: "AdminUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationStatusHistories_Applications_ApplicationId",
                table: "ApplicationStatusHistories",
                column: "ApplicationId",
                principalTable: "Applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentRules_RepresentativeOffices_RepresentativeOfficeId",
                table: "AssignmentRules",
                column: "RepresentativeOfficeId",
                principalTable: "RepresentativeOffices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_AdminUsers_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId",
                principalTable: "AdminUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Districts_Provinces_ProvinceId",
                table: "Districts",
                column: "ProvinceId",
                principalTable: "Provinces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExportLogs_AdminUsers_ActorUserId",
                table: "ExportLogs",
                column: "ActorUserId",
                principalTable: "AdminUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Neighborhoods_Districts_DistrictId",
                table: "Neighborhoods",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Provinces_Regions_RegionId",
                table: "Provinces",
                column: "RegionId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_AdminUsers_AdminUserId",
                table: "RefreshTokens",
                column: "AdminUserId",
                principalTable: "AdminUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityLogs_AdminUsers_ActorUserId",
                table: "SecurityLogs",
                column: "ActorUserId",
                principalTable: "AdminUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationAssignments_AdminUsers_ActorUserId",
                table: "ApplicationAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationAssignments_Applications_ApplicationId",
                table: "ApplicationAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationAssignments_AssignmentRules_AssignmentRuleId",
                table: "ApplicationAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationAssignments_RepresentativeOffices_Representative~",
                table: "ApplicationAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationConsents_Applications_ApplicationId",
                table: "ApplicationConsents");

            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Districts_DistrictId",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Neighborhoods_NeighborhoodId",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Provinces_ProvinceId",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationStatusHistories_AdminUsers_ActorUserId",
                table: "ApplicationStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationStatusHistories_Applications_ApplicationId",
                table: "ApplicationStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentRules_RepresentativeOffices_RepresentativeOfficeId",
                table: "AssignmentRules");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_AdminUsers_ActorUserId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Districts_Provinces_ProvinceId",
                table: "Districts");

            migrationBuilder.DropForeignKey(
                name: "FK_ExportLogs_AdminUsers_ActorUserId",
                table: "ExportLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Neighborhoods_Districts_DistrictId",
                table: "Neighborhoods");

            migrationBuilder.DropForeignKey(
                name: "FK_Provinces_Regions_RegionId",
                table: "Provinces");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_AdminUsers_AdminUserId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityLogs_AdminUsers_ActorUserId",
                table: "SecurityLogs");

            migrationBuilder.DropIndex(
                name: "IX_SecurityLogs_ActorUserId",
                table: "SecurityLogs");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_AdminUserId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Provinces_RegionId",
                table: "Provinces");

            migrationBuilder.DropIndex(
                name: "IX_ExportLogs_ActorUserId",
                table: "ExportLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentRules_RepresentativeOfficeId",
                table: "AssignmentRules");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationStatusHistories_ActorUserId",
                table: "ApplicationStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationStatusHistories_ApplicationId",
                table: "ApplicationStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_Applications_DistrictId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_NeighborhoodId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationConsents_ApplicationId",
                table: "ApplicationConsents");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationAssignments_ActorUserId",
                table: "ApplicationAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationAssignments_AssignmentRuleId",
                table: "ApplicationAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationAssignments_RepresentativeOfficeId",
                table: "ApplicationAssignments");
        }
    }
}
