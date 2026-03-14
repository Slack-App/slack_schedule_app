using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlackActionTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddDueDateToActionItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DueDateText",
                table: "ActionItems",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueDateText",
                table: "ActionItems");
        }
    }
}
