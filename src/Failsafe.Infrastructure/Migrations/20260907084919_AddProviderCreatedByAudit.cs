using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Failsafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderCreatedByAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByName",
                table: "PaymentProviders",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "PaymentProviders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByName",
                table: "PaymentProviders");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "PaymentProviders");
        }
    }
}
