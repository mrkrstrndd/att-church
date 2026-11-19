using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurchAttendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberQrCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "qr_token",
                table: "members",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValueSql: "replace(gen_random_uuid()::text || gen_random_uuid()::text, '-', '')");

            migrationBuilder.CreateIndex(
                name: "IX_members_qr_token",
                table: "members",
                column: "qr_token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_members_qr_token",
                table: "members");

            migrationBuilder.DropColumn(
                name: "qr_token",
                table: "members");
        }
    }
}
