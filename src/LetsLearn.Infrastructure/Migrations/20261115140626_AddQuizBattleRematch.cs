using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsLearn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizBattleRematch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RematchOfBattleId",
                table: "QuizBattles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuizBattles_RematchOfBattleId",
                table: "QuizBattles",
                column: "RematchOfBattleId",
                unique: true,
                filter: "\"RematchOfBattleId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuizBattles_RematchOfBattleId",
                table: "QuizBattles");

            migrationBuilder.DropColumn(
                name: "RematchOfBattleId",
                table: "QuizBattles");
        }
    }
}
