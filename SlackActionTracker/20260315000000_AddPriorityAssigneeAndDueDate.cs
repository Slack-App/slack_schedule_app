using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlackActionTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddPriorityAssigneeAndDueDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Priority: 0=Low, 1=Medium, 2=High — default Medium
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "ActionItems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // AssigneeId: the Slack user ID of whoever is responsible
            migrationBuilder.AddColumn<string>(
                name: "AssigneeId",
                table: "ActionItems",
                type: "text",
                nullable: true);

            // DueDate: structured DateTime for overdue detection (separate from DueDateText)
            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "ActionItems",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Priority",    table: "ActionItems");
            migrationBuilder.DropColumn(name: "AssigneeId",  table: "ActionItems");
            migrationBuilder.DropColumn(name: "DueDate",     table: "ActionItems");
        }
    }
}
