using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsLearn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionAndBattleDifficulty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "QuizBattles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "mixed");

            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "Questions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "medium");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "QuizBattles");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Questions");
        }
    }
}
