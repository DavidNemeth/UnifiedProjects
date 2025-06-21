using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UPortal.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeFinancialDataEnhancment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GrossMonthlyWage",
                table: "AppUsers",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeniorityLevel",
                table: "AppUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SeniorityRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Level = table.Column<int>(type: "int", nullable: false),
                    DailyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeniorityRates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeniorityRates_Level",
                table: "SeniorityRates",
                column: "Level",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeniorityRates");

            migrationBuilder.DropColumn(
                name: "GrossMonthlyWage",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "SeniorityLevel",
                table: "AppUsers");
        }
    }
}
