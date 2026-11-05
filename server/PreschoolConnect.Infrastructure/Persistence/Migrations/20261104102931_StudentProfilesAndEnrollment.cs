using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreschoolConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentProfilesAndEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Students",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Care",
                table: "Students",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Students",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Emergency",
                table: "Students",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Students",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Guardian",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Height",
                table: "Students",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Students",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Pickup",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Relationship",
                table: "Students",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "Students",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Students_SchoolId_Id",
                table: "Students",
                columns: new[] { "SchoolId", "Id" });

            migrationBuilder.CreateTable(
                name: "StudentInClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassroomId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AgeGroup = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StudyMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastAction = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    LastActionByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastActionAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentInClasses", x => x.Id);
                    table.CheckConstraint("CK_StudentInClasses_AuditTime", "\"LastActionAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_StudentInClasses_Dates", "\"EndDate\" > \"StartDate\"");
                    table.CheckConstraint("CK_StudentInClasses_LastAction", "\"LastAction\" IN ('I', 'U', 'D')");
                    table.ForeignKey(
                        name: "FK_StudentInClasses_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentInClasses_AspNetUsers_LastActionByUserId",
                        column: x => x.LastActionByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentInClasses_Classrooms_SchoolId_ClassroomId",
                        columns: x => new { x.SchoolId, x.ClassroomId },
                        principalTable: "Classrooms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentInClasses_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Students_Measurements",
                table: "Students",
                sql: "(\"Height\" IS NULL OR \"Height\" > 0 AND \"Height\" <= 250) AND (\"Weight\" IS NULL OR \"Weight\" > 0 AND \"Weight\" <= 300)");

            migrationBuilder.CreateIndex(
                name: "IX_StudentInClasses_CreatedByUserId",
                table: "StudentInClasses",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentInClasses_LastActionByUserId",
                table: "StudentInClasses",
                column: "LastActionByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentInClasses_SchoolId_ClassroomId",
                table: "StudentInClasses",
                columns: new[] { "SchoolId", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentInClasses_SchoolId_StudentId_StartDate",
                table: "StudentInClasses",
                columns: new[] { "SchoolId", "StudentId", "StartDate" });
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS btree_gist;
                ALTER TABLE "StudentInClasses" ADD CONSTRAINT "EX_StudentInClasses_NoOverlap"
                  EXCLUDE USING gist ("SchoolId" WITH =, "StudentId" WITH =,
                    daterange("StartDate", "EndDate", '[)') WITH &&);
                CREATE TRIGGER "TR_Business_Audit" BEFORE INSERT OR UPDATE OR DELETE ON "StudentInClasses"
                  FOR EACH ROW EXECUTE FUNCTION guard_business_audit();
                CREATE FUNCTION guard_student_relations() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE school_today date := (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Ho_Chi_Minh')::date;
                BEGIN
                  IF TG_TABLE_NAME IN ('Students', 'Classrooms') THEN
                    IF NEW."LastAction" = 'D' AND EXISTS (
                      SELECT 1 FROM "StudentInClasses" e WHERE e."SchoolId" = NEW."SchoolId" AND
                        CASE WHEN TG_TABLE_NAME = 'Students' THEN e."StudentId" ELSE e."ClassroomId" END = NEW."Id"
                    ) THEN
                      RAISE EXCEPTION 'Retained enrollment history prevents deletion'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_StudentInClasses_RetainedParent';
                    END IF;
                    IF TG_TABLE_NAME = 'Students' AND EXISTS (
                      SELECT 1 FROM "StudentInClasses" e WHERE e."StudentId" = NEW."Id"
                        AND e."StartDate" < (to_jsonb(NEW)->>'DateOfBirth')::date
                    ) THEN
                      RAISE EXCEPTION 'Birthday cannot follow enrollment'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_Students_BirthdayEnrollment';
                    END IF;
                    RETURN NEW;
                  END IF;
                  IF NEW."LastAction" = 'D' OR (TG_OP = 'UPDATE' AND (
                    NEW."StudentId" <> OLD."StudentId" OR NEW."ClassroomId" <> OLD."ClassroomId" OR
                    NEW."StartDate" <> OLD."StartDate" OR NEW."AgeGroup" <> OLD."AgeGroup" OR
                    NEW."StudyMode" <> OLD."StudyMode" OR OLD."EndDate" <= school_today OR
                    OLD."StartDate" >= school_today OR NEW."EndDate" <> school_today
                  )) THEN
                    RAISE EXCEPTION 'Enrollment history may only be ended today'
                      USING ERRCODE = '23514', CONSTRAINT = 'CK_StudentInClasses_History';
                  END IF;
                  IF TG_OP = 'INSERT' AND (
                    NEW."StartDate" > school_today OR
                    NEW."AgeGroup" NOT IN ('Mầm · 3–4 tuổi', 'Chồi · 4–5 tuổi', 'Lá · 5–6 tuổi') OR
                    NEW."StudyMode" NOT IN ('Bán trú', 'Cả ngày') OR
                    (NEW."StartDate" <> school_today AND EXISTS (
                      SELECT 1 FROM "StudentInClasses" e WHERE e."SchoolId" = NEW."SchoolId"
                        AND e."StudentId" = NEW."StudentId"
                    )) OR NOT EXISTS (
                      SELECT 1 FROM "Students" s JOIN "Classrooms" c ON c."SchoolId" = s."SchoolId"
                        JOIN "SchoolYears" y ON y."Id" = c."SchoolYearId" AND y."SchoolId" = c."SchoolId"
                      WHERE s."Id" = NEW."StudentId" AND s."SchoolId" = NEW."SchoolId" AND s."LastAction" <> 'D'
                        AND c."Id" = NEW."ClassroomId" AND c."LastAction" <> 'D' AND y."LastAction" <> 'D'
                        AND NEW."StartDate" >= s."DateOfBirth" AND NEW."StartDate" >= y."StartDate"
                        AND NEW."StartDate" <= y."EndDate" AND NEW."EndDate" = y."EndDate" + 1
                    )
                  ) THEN
                    RAISE EXCEPTION 'Enrollment requires live same-school parents and valid year-bounded dates'
                      USING ERRCODE = '23514', CONSTRAINT = 'CK_StudentInClasses_Parents';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_Student_Relations" BEFORE INSERT OR UPDATE ON "StudentInClasses"
                  FOR EACH ROW EXECUTE FUNCTION guard_student_relations();
                CREATE TRIGGER "TR_Student_Relations" BEFORE UPDATE ON "Students"
                  FOR EACH ROW EXECUTE FUNCTION guard_student_relations();
                CREATE TRIGGER "TR_Student_Relations" BEFORE UPDATE ON "Classrooms"
                  FOR EACH ROW EXECUTE FUNCTION guard_student_relations();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Student profiles and enrollment history are forward-only; use a reviewed corrective migration.");
        }
    }
}
