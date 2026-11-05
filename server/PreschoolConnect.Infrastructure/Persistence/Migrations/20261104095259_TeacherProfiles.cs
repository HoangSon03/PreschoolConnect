using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreschoolConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeacherProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Classrooms_SchoolId_Id",
                table: "Classrooms",
                columns: new[] { "SchoolId", "Id" });

            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Birthday = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Phone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Degree = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Graduated = table.Column<int>(type: "integer", nullable: false),
                    University = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Experience = table.Column<int>(type: "integer", nullable: false),
                    Certificate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Contract = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Work = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastAction = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    LastActionByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastActionAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.Id);
                    table.UniqueConstraint("AK_Teachers_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_Teachers_AuditTime", "\"LastActionAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Teachers_Experience", "\"Experience\" >= 0 AND \"Graduated\" >= 1950");
                    table.CheckConstraint("CK_Teachers_LastAction", "\"LastAction\" IN ('I', 'U', 'D')");
                    table.ForeignKey(
                        name: "FK_Teachers_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Teachers_AspNetUsers_LastActionByUserId",
                        column: x => x.LastActionByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Teachers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Teachers_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassroomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastAction = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    LastActionByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastActionAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherAssignments", x => x.Id);
                    table.CheckConstraint("CK_TeacherAssignments_AuditTime", "\"LastActionAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_TeacherAssignments_LastAction", "\"LastAction\" IN ('I', 'U', 'D')");
                    table.CheckConstraint("CK_TeacherAssignments_Time", "\"EndedAtUtc\" IS NULL OR \"EndedAtUtc\" >= \"StartedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_AspNetUsers_LastActionByUserId",
                        column: x => x.LastActionByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_Classrooms_SchoolId_ClassroomId",
                        columns: x => new { x.SchoolId, x.ClassroomId },
                        principalTable: "Classrooms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_Teachers_SchoolId_TeacherId",
                        columns: x => new { x.SchoolId, x.TeacherId },
                        principalTable: "Teachers",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_CreatedByUserId",
                table: "TeacherAssignments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_LastActionByUserId",
                table: "TeacherAssignments",
                column: "LastActionByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_SchoolId_ClassroomId",
                table: "TeacherAssignments",
                columns: new[] { "SchoolId", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_SchoolId_TeacherId_ClassroomId",
                table: "TeacherAssignments",
                columns: new[] { "SchoolId", "TeacherId", "ClassroomId" },
                unique: true,
                filter: "\"EndedAtUtc\" IS NULL AND \"LastAction\" <> 'D'");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_CreatedByUserId",
                table: "Teachers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_LastActionByUserId",
                table: "Teachers",
                column: "LastActionByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_SchoolId_UserId",
                table: "Teachers",
                columns: new[] { "SchoolId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_UserId",
                table: "Teachers",
                column: "UserId");
            AddTeacherGuards(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Teacher profiles and assignment history are forward-only; use a reviewed corrective migration.");
        }

        private static void AddTeacherGuards(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_Business_Audit" BEFORE INSERT OR UPDATE OR DELETE ON "Teachers"
                  FOR EACH ROW EXECUTE FUNCTION guard_business_audit();
                CREATE TRIGGER "TR_Business_Audit" BEFORE INSERT OR UPDATE OR DELETE ON "TeacherAssignments"
                  FOR EACH ROW EXECUTE FUNCTION guard_business_audit();
                CREATE FUNCTION guard_teacher_relations() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_TABLE_NAME = 'Teachers' THEN
                    IF TG_OP = 'UPDATE' AND OLD."UserId" IS NOT NULL AND NEW."UserId" IS DISTINCT FROM OLD."UserId" THEN
                      RAISE EXCEPTION 'Teacher identity cannot be reassigned'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_Teachers_Identity';
                    END IF;
                    IF NEW."UserId" IS NOT NULL AND NEW."LastAction" <> 'D'
                      AND (TG_OP = 'INSERT' OR OLD."UserId" IS DISTINCT FROM NEW."UserId") AND NOT EXISTS (
                      SELECT 1 FROM "AspNetUsers" u JOIN "SchoolMemberships" m ON m."UserId" = u."Id"
                        JOIN "SchoolMembershipRoles" r ON r."MembershipId" = m."Id"
                      WHERE u."Id" = NEW."UserId" AND u."IsActive" AND m."SchoolId" = NEW."SchoolId"
                        AND m."IsActive" AND m."LastAction" <> 'D' AND r."Role" = 'Teacher'
                    ) THEN
                      RAISE EXCEPTION 'Teacher requires an active same-school membership'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_Teachers_Membership';
                    END IF;
                  ELSE
                    IF TG_OP = 'UPDATE' AND (
                      NEW."TeacherId" <> OLD."TeacherId" OR NEW."ClassroomId" <> OLD."ClassroomId" OR
                      NEW."StartedAtUtc" <> OLD."StartedAtUtc" OR NEW."Role" <> OLD."Role" OR
                      OLD."EndedAtUtc" IS NOT NULL OR NEW."EndedAtUtc" IS NULL
                    ) THEN
                      RAISE EXCEPTION 'Assignment history may only be ended once'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_TeacherAssignments_History';
                    END IF;
                    IF TG_OP = 'INSERT' AND (NEW."EndedAtUtc" IS NOT NULL OR NOT EXISTS (
                      SELECT 1 FROM "Teachers" t JOIN "Classrooms" c ON c."SchoolId" = t."SchoolId"
                        JOIN "SchoolYears" y ON y."Id" = c."SchoolYearId" AND y."SchoolId" = c."SchoolId"
                      WHERE t."Id" = NEW."TeacherId" AND t."SchoolId" = NEW."SchoolId" AND t."IsActive"
                        AND t."LastAction" <> 'D' AND c."Id" = NEW."ClassroomId" AND c."LastAction" <> 'D'
                        AND y."LastAction" <> 'D'
                    )) THEN
                      RAISE EXCEPTION 'Assignment requires an active teacher and class/year'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_TeacherAssignments_Parents';
                    END IF;
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_Teacher_Relations" BEFORE INSERT OR UPDATE ON "Teachers"
                  FOR EACH ROW EXECUTE FUNCTION guard_teacher_relations();
                CREATE TRIGGER "TR_Teacher_Relations" BEFORE INSERT OR UPDATE ON "TeacherAssignments"
                  FOR EACH ROW EXECUTE FUNCTION guard_teacher_relations();
                """);
        }

    }
}
