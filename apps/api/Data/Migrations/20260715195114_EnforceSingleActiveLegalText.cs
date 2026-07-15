using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasvuruAkis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleActiveLegalText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LegalTexts_Type_Active",
                table: "LegalTexts",
                column: "Type",
                unique: true,
                filter: "\"IsActive\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LegalTexts_Type_Active",
                table: "LegalTexts");
        }
    }
}
