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
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF to_regclass('public.""Messages""') IS NOT NULL THEN
                        ALTER TABLE ""Messages"" ADD COLUMN IF NOT EXISTS ""ImageUrl"" text;
                        ALTER TABLE ""Messages"" ADD COLUMN IF NOT EXISTS ""FileUrl"" text;
                    END IF;
                END $$;
            ");
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
