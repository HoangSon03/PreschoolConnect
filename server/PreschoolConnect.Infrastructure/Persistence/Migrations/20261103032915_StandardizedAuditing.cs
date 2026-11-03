using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreschoolConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StandardizedAuditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM "StudentAccounts" a LEFT JOIN "AspNetUsers" u ON u."Id" = a."UserId"
                             WHERE u."Id" IS NULL) THEN
                    RAISE EXCEPTION 'Audit migration requires existing student accounts to reference Identity users. Correct orphan links; no data was removed.';
                  END IF;
                  IF EXISTS (SELECT 1 FROM "StudentAccounts" a JOIN "Students" s ON s."Id" = a."StudentId"
                             WHERE a."SchoolId" <> s."SchoolId") THEN
                    RAISE EXCEPTION 'Audit migration found cross-school student-account links. Correct ownership before applying; no data was removed.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_SchoolMembershipRoles_SchoolMemberships_MembershipId",
                table: "SchoolMembershipRoles");

            migrationBuilder.DropIndex(
                name: "IX_Classrooms_SchoolYearId_Name",
                table: "Classrooms");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "Students",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Students",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAction",
                table: "Students",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActionAtUtc",
                table: "Students",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "LastActionByUserId",
                table: "Students",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "StudentAccounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "StudentAccounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAction",
                table: "StudentAccounts",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActionAtUtc",
                table: "StudentAccounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "LastActionByUserId",
                table: "StudentAccounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "SchoolYears",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "SchoolYears",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAction",
                table: "SchoolYears",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActionAtUtc",
                table: "SchoolYears",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "LastActionByUserId",
                table: "SchoolYears",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "Schools",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Schools",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAction",
                table: "Schools",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActionAtUtc",
                table: "Schools",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "LastActionByUserId",
                table: "Schools",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "SchoolMemberships",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "SchoolMemberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAction",
                table: "SchoolMemberships",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActionAtUtc",
                table: "SchoolMemberships",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "LastActionByUserId",
                table: "SchoolMemberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "Classrooms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Classrooms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAction",
                table: "Classrooms",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActionAtUtc",
                table: "Classrooms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "LastActionByUserId",
                table: "Classrooms",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                DO $$
                DECLARE tbl text; baseline timestamptz := transaction_timestamp();
                BEGIN
                  FOREACH tbl IN ARRAY ARRAY['Schools', 'SchoolMemberships', 'Students', 'StudentAccounts', 'SchoolYears', 'Classrooms']
                  LOOP
                    IF tbl IN ('Schools', 'SchoolMemberships', 'Students', 'StudentAccounts') THEN
                      EXECUTE format('UPDATE %I SET "CreatedAtUtc" = "CreatedUtc", "LastActionAtUtc" = GREATEST("CreatedUtc", $1), "LastAction" = ''I''', tbl) USING baseline;
                    ELSE
                      EXECUTE format('UPDATE %I SET "CreatedAtUtc" = $1, "LastActionAtUtc" = $1, "LastAction" = ''I''', tbl) USING baseline;
                    END IF;
                    EXECUTE format('ALTER TABLE %I ALTER COLUMN "CreatedAtUtc" DROP DEFAULT, ALTER COLUMN "LastActionAtUtc" DROP DEFAULT, ALTER COLUMN "LastAction" DROP DEFAULT', tbl);
                  END LOOP;
                END $$;
                """);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SystemOperation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangedFields = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.CheckConstraint("CK_AuditLogs_Action", "\"Action\" IN ('I', 'U', 'D')");
                    table.CheckConstraint("CK_AuditLogs_Actor", "(\"ActorUserId\" IS NOT NULL AND \"SystemOperation\" IS NULL) OR (\"ActorUserId\" IS NULL AND \"SystemOperation\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AuditLogs_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_CreatedByUserId",
                table: "Students",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_LastActionByUserId",
                table: "Students",
                column: "LastActionByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Students_AuditTime",
                table: "Students",
                sql: "\"LastActionAtUtc\" >= \"CreatedAtUtc\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Students_LastAction",
                table: "Students",
                sql: "\"LastAction\" IN ('I', 'U', 'D')");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAccounts_CreatedByUserId",
                table: "StudentAccounts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAccounts_LastActionByUserId",
                table: "StudentAccounts",
                column: "LastActionByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudentAccounts_AuditTime",
                table: "StudentAccounts",
                sql: "\"LastActionAtUtc\" >= \"CreatedAtUtc\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudentAccounts_LastAction",
                table: "StudentAccounts",
                sql: "\"LastAction\" IN ('I', 'U', 'D')");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolYears_CreatedByUserId",
                table: "SchoolYears",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolYears_LastActionByUserId",
                table: "SchoolYears",
                column: "LastActionByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SchoolYears_AuditTime",
                table: "SchoolYears",
                sql: "\"LastActionAtUtc\" >= \"CreatedAtUtc\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SchoolYears_LastAction",
                table: "SchoolYears",
                sql: "\"LastAction\" IN ('I', 'U', 'D')");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_CreatedByUserId",
                table: "Schools",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_LastActionByUserId",
                table: "Schools",
                column: "LastActionByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Schools_AuditTime",
                table: "Schools",
                sql: "\"LastActionAtUtc\" >= \"CreatedAtUtc\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Schools_LastAction",
                table: "Schools",
                sql: "\"LastAction\" IN ('I', 'U', 'D')");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolMemberships_CreatedByUserId",
                table: "SchoolMemberships",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolMemberships_LastActionByUserId",
                table: "SchoolMemberships",
                column: "LastActionByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SchoolMemberships_AuditTime",
                table: "SchoolMemberships",
                sql: "\"LastActionAtUtc\" >= \"CreatedAtUtc\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SchoolMemberships_LastAction",
                table: "SchoolMemberships",
                sql: "\"LastAction\" IN ('I', 'U', 'D')");

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_CreatedByUserId",
                table: "Classrooms",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_LastActionByUserId",
                table: "Classrooms",
                column: "LastActionByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_SchoolYearId_Name",
                table: "Classrooms",
                columns: new[] { "SchoolYearId", "Name" },
                unique: true,
                filter: "\"LastAction\" <> 'D'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Classrooms_AuditTime",
                table: "Classrooms",
                sql: "\"LastActionAtUtc\" >= \"CreatedAtUtc\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Classrooms_LastAction",
                table: "Classrooms",
                sql: "\"LastAction\" IN ('I', 'U', 'D')");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_SchoolId_OccurredAtUtc",
                table: "AuditLogs",
                columns: new[] { "SchoolId", "OccurredAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Classrooms_AspNetUsers_CreatedByUserId",
                table: "Classrooms",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Classrooms_AspNetUsers_LastActionByUserId",
                table: "Classrooms",
                column: "LastActionByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolMembershipRoles_SchoolMemberships_MembershipId",
                table: "SchoolMembershipRoles",
                column: "MembershipId",
                principalTable: "SchoolMemberships",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolMemberships_AspNetUsers_CreatedByUserId",
                table: "SchoolMemberships",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolMemberships_AspNetUsers_LastActionByUserId",
                table: "SchoolMemberships",
                column: "LastActionByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_AspNetUsers_CreatedByUserId",
                table: "Schools",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_AspNetUsers_LastActionByUserId",
                table: "Schools",
                column: "LastActionByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolYears_AspNetUsers_CreatedByUserId",
                table: "SchoolYears",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolYears_AspNetUsers_LastActionByUserId",
                table: "SchoolYears",
                column: "LastActionByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentAccounts_AspNetUsers_CreatedByUserId",
                table: "StudentAccounts",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentAccounts_AspNetUsers_LastActionByUserId",
                table: "StudentAccounts",
                column: "LastActionByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentAccounts_AspNetUsers_UserId",
                table: "StudentAccounts",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_AspNetUsers_CreatedByUserId",
                table: "Students",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_AspNetUsers_LastActionByUserId",
                table: "Students",
                column: "LastActionByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                DROP INDEX "UX_SchoolYears_NormalizedName";
                CREATE UNIQUE INDEX "UX_SchoolYears_NormalizedName"
                  ON "SchoolYears" ("SchoolId", lower(btrim("Name") COLLATE "pg_c_utf8"))
                  WHERE "LastAction" <> 'D';

                CREATE FUNCTION guard_business_audit() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE school uuid; rowdata jsonb; previous jsonb;
                BEGIN
                  IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Physical deletion of audited business records is forbidden'
                      USING ERRCODE = '23514', CONSTRAINT = 'CK_Business_NoPhysicalDelete';
                  END IF;
                  rowdata := to_jsonb(NEW);
                  IF TG_OP = 'INSERT' THEN
                    IF NEW."LastAction" <> 'I' OR NEW."CreatedByUserId" IS DISTINCT FROM NEW."LastActionByUserId"
                       OR NEW."CreatedAtUtc" <> NEW."LastActionAtUtc" THEN
                      RAISE EXCEPTION 'Insert audit metadata is inconsistent'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_Business_InsertAudit';
                    END IF;
                  ELSE
                    previous := to_jsonb(OLD);
                    IF OLD."LastAction" = 'D' THEN
                      RAISE EXCEPTION 'Deleted records cannot be updated or restored'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_Business_DeletedImmutable';
                    END IF;
                    IF NEW."CreatedAtUtc" <> OLD."CreatedAtUtc" OR NEW."CreatedByUserId" IS DISTINCT FROM OLD."CreatedByUserId"
                       OR rowdata->'CreatedUtc' IS DISTINCT FROM previous->'CreatedUtc'
                       OR NEW."Id" <> OLD."Id" OR rowdata->'SchoolId' IS DISTINCT FROM previous->'SchoolId'
                       OR (TG_TABLE_NAME IN ('SchoolMemberships', 'StudentAccounts') AND rowdata->'UserId' IS DISTINCT FROM previous->'UserId')
                       OR (TG_TABLE_NAME = 'StudentAccounts' AND rowdata->'StudentId' IS DISTINCT FROM previous->'StudentId')
                       OR NEW."LastAction" NOT IN ('U', 'D') OR NEW."LastActionAtUtc" <= OLD."LastActionAtUtc" THEN
                      RAISE EXCEPTION 'Update must preserve creation/ownership and advance audit metadata'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_Business_UpdateAudit';
                    END IF;
                  END IF;
                  school := CASE WHEN TG_TABLE_NAME = 'Schools' THEN NEW."Id" ELSE (rowdata->>'SchoolId')::uuid END;
                  PERFORM pg_advisory_xact_lock(hashtextextended('audit-school:' || school::text, 0));
                  IF TG_TABLE_NAME <> 'Schools' AND NEW."LastAction" <> 'D' AND NOT EXISTS (
                    SELECT 1 FROM "Schools" s WHERE s."Id" = school AND s."LastAction" <> 'D' AND s."IsActive"
                  ) THEN
                    RAISE EXCEPTION 'An active school is required'
                      USING ERRCODE = '23514', CONSTRAINT = 'CK_Business_ActiveSchool';
                  END IF;
                  IF TG_TABLE_NAME = 'Schools' AND NEW."LastAction" = 'D' AND (
                    EXISTS (SELECT 1 FROM "SchoolYears" WHERE "SchoolId" = school) OR
                    EXISTS (SELECT 1 FROM "Classrooms" WHERE "SchoolId" = school) OR
                    EXISTS (SELECT 1 FROM "SchoolMemberships" WHERE "SchoolId" = school) OR
                    EXISTS (SELECT 1 FROM "Students" WHERE "SchoolId" = school) OR
                    EXISTS (SELECT 1 FROM "StudentAccounts" WHERE "SchoolId" = school)
                  ) THEN
                    RAISE EXCEPTION 'School has retained business records'
                      USING ERRCODE = '23514', CONSTRAINT = 'CK_Business_RetainedDependents';
                  END IF;
                  IF TG_TABLE_NAME = 'Students' AND NEW."LastAction" = 'D' AND EXISTS (
                    SELECT 1 FROM "StudentAccounts" WHERE "StudentId" = NEW."Id"
                  ) THEN
                    RAISE EXCEPTION 'Student has a retained account mapping'
                      USING ERRCODE = '23514', CONSTRAINT = 'CK_Business_RetainedDependents';
                  END IF;
                  IF TG_TABLE_NAME = 'StudentAccounts' AND NEW."LastAction" <> 'D' AND NOT EXISTS (
                    SELECT 1 FROM "Students" s WHERE s."Id" = (rowdata->>'StudentId')::uuid
                      AND s."SchoolId" = school AND s."LastAction" <> 'D'
                  ) THEN
                    RAISE EXCEPTION 'Student account requires a non-deleted student in the same school'
                      USING ERRCODE = '23514', CONSTRAINT = 'CK_Business_ActiveStudent';
                  END IF;
                  RETURN NEW;
                END $$;

                DO $$
                DECLARE tbl text;
                BEGIN
                  FOREACH tbl IN ARRAY ARRAY['Schools', 'SchoolMemberships', 'Students', 'StudentAccounts', 'SchoolYears', 'Classrooms']
                  LOOP
                    EXECUTE format('CREATE TRIGGER "TR_Business_Audit" BEFORE INSERT OR UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION guard_business_audit()', tbl);
                  END LOOP;
                END $$;

                CREATE OR REPLACE FUNCTION guard_school_year() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Physical deletion is forbidden'
                      USING ERRCODE = '23514', CONSTRAINT = 'CK_Business_NoPhysicalDelete';
                  END IF;
                  PERFORM pg_advisory_xact_lock(hashtextextended('school-year:' || NEW."SchoolId"::text, 0));
                  IF TG_OP = 'UPDATE' AND (
                    NEW."LastAction" = 'D' OR NEW."StartDate" <> OLD."StartDate" OR NEW."EndDate" <> OLD."EndDate"
                  ) AND EXISTS (SELECT 1 FROM "Classrooms" WHERE "SchoolId" = NEW."SchoolId" AND "SchoolYearId" = NEW."Id") THEN
                    RAISE EXCEPTION 'School year has retained classes'
                      USING ERRCODE = '23514', CONSTRAINT = 'CK_SchoolYears_DatesInUse';
                  END IF;
                  IF NEW."LastAction" <> 'D' AND EXISTS (
                    SELECT 1 FROM "SchoolYears" y WHERE y."SchoolId" = NEW."SchoolId" AND y."Id" <> NEW."Id"
                      AND y."LastAction" <> 'D' AND y."StartDate" <= NEW."EndDate" AND y."EndDate" >= NEW."StartDate"
                  ) THEN
                    RAISE EXCEPTION 'School-year dates overlap'
                      USING ERRCODE = '23514', CONSTRAINT = 'CK_SchoolYears_NoOverlap';
                  END IF;
                  RETURN NEW;
                END $$;

                CREATE OR REPLACE FUNCTION lock_class_school_year() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP IN ('UPDATE', 'DELETE') THEN
                    PERFORM pg_advisory_xact_lock(hashtextextended('school-year:' || OLD."SchoolId"::text, 0));
                  END IF;
                  IF TG_OP IN ('INSERT', 'UPDATE') THEN
                    PERFORM pg_advisory_xact_lock(hashtextextended('school-year:' || NEW."SchoolId"::text, 0));
                    IF NEW."LastAction" <> 'D' AND NOT EXISTS (
                      SELECT 1 FROM "SchoolYears" WHERE "SchoolId" = NEW."SchoolId" AND "Id" = NEW."SchoolYearId" AND "LastAction" <> 'D'
                    ) THEN
                      RAISE EXCEPTION 'Class requires a non-deleted school year'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_Classrooms_ActiveSchoolYear';
                    END IF;
                    RETURN NEW;
                  END IF;
                  RETURN OLD;
                END $$;

                CREATE FUNCTION guard_audit_log() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  RAISE EXCEPTION 'AuditLogs are append-only'
                    USING ERRCODE = '23514', CONSTRAINT = 'CK_AuditLogs_AppendOnly';
                END $$;
                CREATE TRIGGER "TR_AuditLogs_AppendOnly" BEFORE UPDATE OR DELETE ON "AuditLogs"
                  FOR EACH ROW EXECUTE FUNCTION guard_audit_log();

                CREATE FUNCTION guard_membership_grant() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE membership uuid; school uuid;
                BEGIN
                  membership := CASE WHEN TG_OP = 'DELETE' THEN OLD."MembershipId" ELSE NEW."MembershipId" END;
                  SELECT "SchoolId" INTO school FROM "SchoolMemberships" WHERE "Id" = membership;
                  PERFORM pg_advisory_xact_lock(hashtextextended('audit-school:' || school::text, 0));
                  IF NOT EXISTS (SELECT 1 FROM "SchoolMemberships" WHERE "Id" = membership AND "LastAction" <> 'D') THEN
                    RAISE EXCEPTION 'Permission changes require a non-deleted membership'
                      USING ERRCODE = '23514', CONSTRAINT = 'CK_MembershipRoles_ActiveMembership';
                  END IF;
                  IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_MembershipRoles_ActiveMembership" BEFORE INSERT OR UPDATE OR DELETE ON "SchoolMembershipRoles"
                  FOR EACH ROW EXECUTE FUNCTION guard_membership_grant();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Downgrading would remove audit history and expose tombstones as active records.
            migrationBuilder.Sql("DO $$ BEGIN RAISE EXCEPTION 'StandardizedAuditing is forward-only. Use a reviewed forward migration; audit history must not be dropped.'; END $$;");
        }
    }
}
