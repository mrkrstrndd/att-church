using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurchAttendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberInviter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "invited_by_member_id",
                table: "members",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_members_invited_by",
                table: "members",
                column: "invited_by_member_id");

            migrationBuilder.AddForeignKey(
                name: "FK_members_members_invited_by_member_id",
                table: "members",
                column: "invited_by_member_id",
                principalTable: "members",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_members_members_invited_by_member_id",
                table: "members");

            migrationBuilder.DropIndex(
                name: "ix_members_invited_by",
                table: "members");

            migrationBuilder.DropColumn(
                name: "invited_by_member_id",
                table: "members");
        }
    }
}
