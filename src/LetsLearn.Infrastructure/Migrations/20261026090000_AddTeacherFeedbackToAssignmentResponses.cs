using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsLearn.Infrastructure.Migrations
{
    [DbContext(typeof(LetsLearnContext))]
    [Migration("20261026090000_AddTeacherFeedbackToAssignmentResponses")]
    public partial class AddTeacherFeedbackToAssignmentResponses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TeacherFeedback",
                table: "AssignmentResponses",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeacherFeedback",
                table: "AssignmentResponses");
        }
    }
}
