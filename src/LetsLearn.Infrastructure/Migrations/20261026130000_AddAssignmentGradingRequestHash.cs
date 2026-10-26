using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsLearn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentGradingRequestHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "AiAssignmentGradingJobs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql("UPDATE \"AiAssignmentGradingJobs\" SET \"RequestHash\" = md5('legacy:' || \"Id\"::text) WHERE \"RequestHash\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "RequestHash",
                table: "AiAssignmentGradingJobs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentGradingJobs_RequestHash",
                table: "AiAssignmentGradingJobs",
                column: "RequestHash",
                unique: true,
                filter: "\"Status\" IN ('queued', 'processing', 'succeeded')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiAssignmentGradingJobs_RequestHash",
                table: "AiAssignmentGradingJobs");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "AiAssignmentGradingJobs");
        }
    }
}
