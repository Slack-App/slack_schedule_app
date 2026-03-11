using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlackActionTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddingCompletedAtField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "ActionItems",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "ActionItems");
        }
    }
}
