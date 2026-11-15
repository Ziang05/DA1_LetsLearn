using System;
using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsLearn.Infrastructure.Migrations;

[DbContext(typeof(LetsLearnContext))]
[Migration("20261112090000_AddQuizBattlesAndLeaderboard")]
public partial class AddQuizBattlesAndLeaderboard : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("QuizBattles", table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            JoinCode = table.Column<string>(type: "text", nullable: false),
            HostId = table.Column<Guid>(type: "uuid", nullable: false),
            CourseId = table.Column<string>(type: "text", nullable: false),
            Status = table.Column<string>(type: "text", nullable: false),
            QuestionCount = table.Column<int>(type: "integer", nullable: false),
            SecondsPerQuestion = table.Column<int>(type: "integer", nullable: false),
            QuestionsJson = table.Column<string>(type: "text", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_QuizBattles", x => x.Id);
            table.ForeignKey("FK_QuizBattles_Users_HostId", x => x.HostId, "Users", "Id", onDelete: ReferentialAction.Cascade);
        });

        migrationBuilder.CreateTable("QuizBattleParticipants", table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            BattleId = table.Column<Guid>(type: "uuid", nullable: false),
            UserId = table.Column<Guid>(type: "uuid", nullable: false),
            JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            Score = table.Column<int>(type: "integer", nullable: false),
            TotalAnswerMilliseconds = table.Column<long>(type: "bigint", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_QuizBattleParticipants", x => x.Id);
            table.ForeignKey("FK_QuizBattleParticipants_QuizBattles_BattleId", x => x.BattleId, "QuizBattles", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_QuizBattleParticipants_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
        });

        migrationBuilder.CreateTable("QuizBattleAnswers", table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            BattleId = table.Column<Guid>(type: "uuid", nullable: false),
            UserId = table.Column<Guid>(type: "uuid", nullable: false),
            QuestionIndex = table.Column<int>(type: "integer", nullable: false),
            ChoiceId = table.Column<Guid>(type: "uuid", nullable: true),
            IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
            AnswerMilliseconds = table.Column<long>(type: "bigint", nullable: false),
            SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_QuizBattleAnswers", x => x.Id);
            table.ForeignKey("FK_QuizBattleAnswers_QuizBattles_BattleId", x => x.BattleId, "QuizBattles", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_QuizBattleAnswers_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
        });

        migrationBuilder.CreateIndex("IX_QuizBattles_HostId", "QuizBattles", "HostId");
        migrationBuilder.CreateIndex("IX_QuizBattles_JoinCode", "QuizBattles", "JoinCode", unique: true);
        migrationBuilder.CreateIndex("IX_QuizBattles_Status_CreatedAt", "QuizBattles", new[] { "Status", "CreatedAt" });
        migrationBuilder.CreateIndex("IX_QuizBattleParticipants_BattleId_UserId", "QuizBattleParticipants", new[] { "BattleId", "UserId" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuizBattleParticipants_UserId", "QuizBattleParticipants", "UserId");
        migrationBuilder.CreateIndex("IX_QuizBattleAnswers_BattleId_UserId_QuestionIndex", "QuizBattleAnswers", new[] { "BattleId", "UserId", "QuestionIndex" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuizBattleAnswers_UserId", "QuizBattleAnswers", "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("QuizBattleAnswers");
        migrationBuilder.DropTable("QuizBattleParticipants");
        migrationBuilder.DropTable("QuizBattles");
    }
}
