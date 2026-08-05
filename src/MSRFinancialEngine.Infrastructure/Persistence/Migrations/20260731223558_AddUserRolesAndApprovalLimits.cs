using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSRFinancialEngine.Infrastructure.Persistence.Migrations
{
    public partial class AddUserRolesAndApprovalLimits : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ApprovalLimitAmount",
                table: "users",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_users_CompanyId",
                table: "users",
                column: "CompanyId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_CompanyId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ApprovalLimitAmount",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "users");
        }
    }
}
