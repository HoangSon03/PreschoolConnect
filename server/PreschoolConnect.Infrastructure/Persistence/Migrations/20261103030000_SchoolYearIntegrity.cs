using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PreschoolConnect.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261103030000_SchoolYearIntegrity")]
public class SchoolYearIntegrity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
              IF EXISTS (
                SELECT 1 FROM "SchoolYears" a JOIN "SchoolYears" b
                  ON a."SchoolId" = b."SchoolId" AND a."Id" < b."Id"
                WHERE lower(btrim(a."Name") COLLATE "pg_c_utf8") = lower(btrim(b."Name") COLLATE "pg_c_utf8")
                   OR (a."StartDate" <= b."EndDate" AND b."StartDate" <= a."EndDate")
              ) THEN
                RAISE EXCEPTION 'School-year names or date ranges conflict. Correct existing records before applying SchoolYearIntegrity; no records were changed.';
              END IF;
            END $$;

            CREATE UNIQUE INDEX "UX_SchoolYears_NormalizedName"
              ON "SchoolYears" ("SchoolId", lower(btrim("Name") COLLATE "pg_c_utf8"));

            CREATE FUNCTION guard_school_year() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE school uuid;
            BEGIN
              school := CASE WHEN TG_OP = 'DELETE' THEN OLD."SchoolId" ELSE NEW."SchoolId" END;
              -- Share this lock with class writes so checks remain true under concurrent edits.
              PERFORM pg_advisory_xact_lock(hashtextextended('school-year:' || school::text, 0));
              IF TG_OP = 'UPDATE' AND (NEW."SchoolId" <> OLD."SchoolId" OR NEW."Id" <> OLD."Id") THEN
                RAISE EXCEPTION 'School-year ownership and identity cannot change';
              END IF;
              IF TG_OP = 'DELETE' OR (TG_OP = 'UPDATE' AND
                (NEW."StartDate" <> OLD."StartDate" OR NEW."EndDate" <> OLD."EndDate")) THEN
                IF EXISTS (SELECT 1 FROM "Classrooms" WHERE "SchoolId" = school AND "SchoolYearId" = OLD."Id") THEN
                  RAISE EXCEPTION 'School year has classes'
                    USING ERRCODE = '23514', CONSTRAINT = 'CK_SchoolYears_DatesInUse';
                END IF;
              END IF;
              IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
              IF EXISTS (
                SELECT 1 FROM "SchoolYears" y WHERE y."SchoolId" = school AND y."Id" <> NEW."Id"
                  AND y."StartDate" <= NEW."EndDate" AND y."EndDate" >= NEW."StartDate"
              ) THEN
                RAISE EXCEPTION 'School-year dates overlap'
                  USING ERRCODE = '23514', CONSTRAINT = 'CK_SchoolYears_NoOverlap';
              END IF;
              RETURN NEW;
            END $$;

            CREATE TRIGGER "TR_SchoolYears_Integrity" BEFORE INSERT OR UPDATE OR DELETE ON "SchoolYears"
              FOR EACH ROW EXECUTE FUNCTION guard_school_year();

            CREATE FUNCTION lock_class_school_year() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF TG_OP IN ('UPDATE', 'DELETE') THEN
                PERFORM pg_advisory_xact_lock(hashtextextended('school-year:' || OLD."SchoolId"::text, 0));
              END IF;
              IF TG_OP IN ('INSERT', 'UPDATE') THEN
                PERFORM pg_advisory_xact_lock(hashtextextended('school-year:' || NEW."SchoolId"::text, 0));
                RETURN NEW;
              END IF;
              RETURN OLD;
            END $$;

            CREATE TRIGGER "TR_Classrooms_SchoolYearLock" BEFORE INSERT OR UPDATE OR DELETE ON "Classrooms"
              FOR EACH ROW EXECUTE FUNCTION lock_class_school_year();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER "TR_Classrooms_SchoolYearLock" ON "Classrooms";
            DROP FUNCTION lock_class_school_year();
            DROP TRIGGER "TR_SchoolYears_Integrity" ON "SchoolYears";
            DROP FUNCTION guard_school_year();
            DROP INDEX "UX_SchoolYears_NormalizedName";
            """);
    }
}
