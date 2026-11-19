using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurchAttendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVipRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "id", "name" },
                values: new object[] { (short)4, "VIP" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: (short)4);
        }
    }
}
