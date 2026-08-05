using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSRFinancialEngine.Infrastructure.Persistence.Migrations
{
    public partial class AddAuditEventArchivedMark : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAtUtc",
                table: "audit_events",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "audit_events");
        }
    }
}
