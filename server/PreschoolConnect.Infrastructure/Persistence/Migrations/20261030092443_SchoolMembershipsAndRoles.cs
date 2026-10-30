using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreschoolConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SchoolMembershipsAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchoolMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolMemberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolMemberships_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchoolMembershipRoles",
                columns: table => new
                {
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolMembershipRoles", x => new { x.MembershipId, x.Role });
                    table.CheckConstraint("CK_SchoolMembershipRoles_Role", "\"Role\" IN ('SchoolAdmin', 'Teacher', 'Parent')");
                    table.ForeignKey(
                        name: "FK_SchoolMembershipRoles_SchoolMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "SchoolMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolMemberships_SchoolId_UserId",
                table: "SchoolMemberships",
                columns: new[] { "SchoolId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolMemberships_UserId_IsActive",
                table: "SchoolMemberships",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolMembershipRoles");

            migrationBuilder.DropTable(
                name: "SchoolMemberships");
        }
    }
}
