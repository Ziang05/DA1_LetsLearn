using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsLearn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaToMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileUrl",
                table: "Messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "FileUrl",
                table: "Messages");
        }
    }
}
