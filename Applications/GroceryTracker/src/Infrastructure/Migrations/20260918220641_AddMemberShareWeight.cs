using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CINE.GroceryTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberShareWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ShareWeight",
                table: "Members",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 1m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShareWeight",
                table: "Members");
        }
    }
}
