using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PreschoolConnect.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261104080000_ClassroomIntegrity")]
public class ClassroomIntegrity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
              IF EXISTS (
                SELECT 1 FROM "Classrooms"
                WHERE "LastAction" <> 'D'
                GROUP BY "SchoolId", "SchoolYearId", lower(btrim("Name") COLLATE "pg_c_utf8")
                HAVING count(*) > 1
              ) THEN
                RAISE EXCEPTION 'Classroom names conflict. Correct existing records before applying ClassroomIntegrity; no records were changed.';
              END IF;
            END $$;

            CREATE UNIQUE INDEX "UX_Classrooms_NormalizedName"
              ON "Classrooms" ("SchoolId", "SchoolYearId", lower(btrim("Name") COLLATE "pg_c_utf8"))
              WHERE "LastAction" <> 'D';

            CREATE FUNCTION guard_classroom_year() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF NEW."SchoolYearId" IS DISTINCT FROM OLD."SchoolYearId" THEN
                RAISE EXCEPTION 'A classroom original school year cannot change'
                  USING ERRCODE = '23514', CONSTRAINT = 'CK_Classrooms_ImmutableSchoolYear';
              END IF;
              RETURN NEW;
            END $$;

            CREATE TRIGGER "TR_Classrooms_ImmutableSchoolYear" BEFORE UPDATE ON "Classrooms"
              FOR EACH ROW EXECUTE FUNCTION guard_classroom_year();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER "TR_Classrooms_ImmutableSchoolYear" ON "Classrooms";
            DROP FUNCTION guard_classroom_year();
            DROP INDEX "UX_Classrooms_NormalizedName";
            """);
    }
}
