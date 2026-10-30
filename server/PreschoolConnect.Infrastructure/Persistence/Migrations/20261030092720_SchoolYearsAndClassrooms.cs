using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreschoolConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SchoolYearsAndClassrooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchoolYears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolYears", x => x.Id);
                    table.UniqueConstraint("AK_SchoolYears_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_SchoolYears_DateRange", "\"EndDate\" > \"StartDate\"");
                    table.ForeignKey(
                        name: "FK_SchoolYears_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Classrooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classrooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Classrooms_SchoolYears_SchoolId_SchoolYearId",
                        columns: x => new { x.SchoolId, x.SchoolYearId },
                        principalTable: "SchoolYears",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Classrooms_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_SchoolId_SchoolYearId",
                table: "Classrooms",
                columns: new[] { "SchoolId", "SchoolYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_SchoolYearId_Name",
                table: "Classrooms",
                columns: new[] { "SchoolYearId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Classrooms");

            migrationBuilder.DropTable(
                name: "SchoolYears");
        }
    }
}
