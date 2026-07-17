using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Service1.Migrations
{
    /// <inheritdoc />
    public partial class addTokenIdForInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TokenId",
                table: "InventoryReservations",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TokenId",
                table: "InventoryReservations");
        }
    }
}
