using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TurnoSync.Infra.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    country_code = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employees", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shifts",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    employee_id = table.Column<int>(type: "INTEGER", nullable: false),
                    date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    starts_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ends_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shifts", x => x.id);
                    table.ForeignKey(
                        name: "fk_shifts_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "employees",
                columns: new[] { "id", "country_code", "email", "is_deleted", "name" },
                values: new object[,]
                {
                    { 1, "ES", "ana@turnosync.dev", false, "Ana Torres" },
                    { 2, "BR", "joao@turnosync.dev", false, "João Lima" },
                    { 3, "AR", "lucia@turnosync.dev", false, "Lucía Fernández" },
                    { 4, "MX", "carlos@turnosync.dev", false, "Carlos Mendoza" },
                    { 5, "PT", "ines@turnosync.dev", false, "Inês Costa" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_employees_email",
                table: "employees",
                column: "email",
                unique: true,
                filter: "is_deleted = 0");

            migrationBuilder.CreateIndex(
                name: "ix_shifts_employee_id_date",
                table: "shifts",
                columns: new[] { "employee_id", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shifts");

            migrationBuilder.DropTable(
                name: "employees");
        }
    }
}
