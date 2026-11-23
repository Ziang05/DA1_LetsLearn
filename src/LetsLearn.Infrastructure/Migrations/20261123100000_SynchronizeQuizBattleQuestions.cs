using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsLearn.Infrastructure.Migrations;

[DbContext(typeof(LetsLearnContext))]
[Migration("20261123100000_SynchronizeQuizBattleQuestions")]
public partial class SynchronizeQuizBattleQuestions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "CurrentQuestionIndex", table: "QuizBattles", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<DateTime>(name: "QuestionStartedAt", table: "QuizBattles", type: "timestamp with time zone", nullable: true);
        migrationBuilder.Sql("UPDATE \"QuizBattles\" SET \"QuestionStartedAt\" = \"StartedAt\" WHERE \"Status\" = 'active' AND \"StartedAt\" IS NOT NULL");
        migrationBuilder.AddColumn<int>(name: "Streak", table: "QuizBattleParticipants", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<bool>(name: "RocketUsed", table: "QuizBattleParticipants", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>(name: "PointsAwarded", table: "QuizBattleAnswers", type: "integer", nullable: false, defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CurrentQuestionIndex", table: "QuizBattles");
        migrationBuilder.DropColumn(name: "QuestionStartedAt", table: "QuizBattles");
        migrationBuilder.DropColumn(name: "Streak", table: "QuizBattleParticipants");
        migrationBuilder.DropColumn(name: "RocketUsed", table: "QuizBattleParticipants");
        migrationBuilder.DropColumn(name: "PointsAwarded", table: "QuizBattleAnswers");
    }
}
