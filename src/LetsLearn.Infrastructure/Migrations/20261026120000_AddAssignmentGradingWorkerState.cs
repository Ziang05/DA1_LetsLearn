using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsLearn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentGradingWorkerState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignmentDescriptionSnapshot",
                table: "AiAssignmentGradingJobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableAt",
                table: "AiAssignmentGradingJobs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseToken",
                table: "AiAssignmentGradingJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentGradingJobs_Status_AvailableAt",
                table: "AiAssignmentGradingJobs",
                columns: new[] { "Status", "AvailableAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiAssignmentGradingJobs_Status_AvailableAt",
                table: "AiAssignmentGradingJobs");

            migrationBuilder.DropColumn(
                name: "AssignmentDescriptionSnapshot",
                table: "AiAssignmentGradingJobs");

            migrationBuilder.DropColumn(
                name: "AvailableAt",
                table: "AiAssignmentGradingJobs");

            migrationBuilder.DropColumn(
                name: "LeaseToken",
                table: "AiAssignmentGradingJobs");
        }
    }
}
