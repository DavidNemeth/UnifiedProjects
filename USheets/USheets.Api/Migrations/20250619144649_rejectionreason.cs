using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USheets.Api.Migrations
{
    /// <inheritdoc />
    public partial class rejectionreason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "TimesheetEntries",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "TimesheetEntries");
        }
    }
}
