using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsLearn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAssignmentGradingPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssignmentRubrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    MaxScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    TeacherGuidance = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentRubrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentRubrics_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignmentRubrics_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiAssignmentGradingJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentResponseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RubricId = table.Column<Guid>(type: "uuid", nullable: false),
                    RubricVersion = table.Column<int>(type: "integer", nullable: false),
                    SubmissionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "queued"),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAssignmentGradingJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiAssignmentGradingJobs_AssignmentResponses_AssignmentRespo~",
                        column: x => x.AssignmentResponseId,
                        principalTable: "AssignmentResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiAssignmentGradingJobs_AssignmentRubrics_RubricId",
                        column: x => x.RubricId,
                        principalTable: "AssignmentRubrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiAssignmentGradingJobs_Users_RequestedBy",
                        column: x => x.RequestedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentRubricCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RubricId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MaxScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentRubricCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentRubricCriteria_AssignmentRubrics_RubricId",
                        column: x => x.RubricId,
                        principalTable: "AssignmentRubrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiAssignmentGradeDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuggestedScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CriterionResultsJson = table.Column<string>(type: "jsonb", nullable: false),
                    SummaryFeedback = table.Column<string>(type: "text", nullable: false),
                    StrengthsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ImprovementsJson = table.Column<string>(type: "jsonb", nullable: false),
                    StudySuggestionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    FinalScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    FinalFeedback = table.Column<string>(type: "text", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAssignmentGradeDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiAssignmentGradeDrafts_AiAssignmentGradingJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "AiAssignmentGradingJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiAssignmentGradeDrafts_Users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentGradeDrafts_JobId",
                table: "AiAssignmentGradeDrafts",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentGradeDrafts_ReviewedBy",
                table: "AiAssignmentGradeDrafts",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentGradeDrafts_ReviewStatus",
                table: "AiAssignmentGradeDrafts",
                column: "ReviewStatus");

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentGradingJobs_AssignmentResponseId_CreatedAt",
                table: "AiAssignmentGradingJobs",
                columns: new[] { "AssignmentResponseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentGradingJobs_InputHash",
                table: "AiAssignmentGradingJobs",
                column: "InputHash",
                unique: true,
                filter: "\"Status\" IN ('queued', 'processing', 'succeeded')");

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentGradingJobs_RequestedBy",
                table: "AiAssignmentGradingJobs",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentGradingJobs_RubricId",
                table: "AiAssignmentGradingJobs",
                column: "RubricId");

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentGradingJobs_Status_CreatedAt",
                table: "AiAssignmentGradingJobs",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRubricCriteria_RubricId_SortOrder",
                table: "AssignmentRubricCriteria",
                columns: new[] { "RubricId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRubrics_CreatedBy",
                table: "AssignmentRubrics",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRubrics_TopicId_Version",
                table: "AssignmentRubrics",
                columns: new[] { "TopicId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAssignmentGradeDrafts");

            migrationBuilder.DropTable(
                name: "AssignmentRubricCriteria");

            migrationBuilder.DropTable(
                name: "AiAssignmentGradingJobs");

            migrationBuilder.DropTable(
                name: "AssignmentRubrics");
        }
    }
}
