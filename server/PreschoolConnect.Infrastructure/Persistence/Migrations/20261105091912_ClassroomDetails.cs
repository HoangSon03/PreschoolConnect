using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreschoolConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClassroomDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "Classrooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Room",
                table: "Classrooms",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ScheduleJson",
                table: "Classrooms",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "WeeklyNotesJson",
                table: "Classrooms",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Classrooms_Capacity",
                table: "Classrooms",
                sql: "\"Capacity\" IS NULL OR \"Capacity\" BETWEEN 1 AND 500");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Classrooms_ScheduleJson",
                table: "Classrooms",
                sql: "jsonb_typeof(\"ScheduleJson\") = 'array' AND jsonb_array_length(\"ScheduleJson\") <= 20");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Classrooms_WeeklyNotesJson",
                table: "Classrooms",
                sql: "jsonb_typeof(\"WeeklyNotesJson\") = 'array' AND jsonb_array_length(\"WeeklyNotesJson\") <= 52");
            migrationBuilder.Sql("""
                CREATE FUNCTION guard_classroom_details() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE item jsonb; previous_end time; starts time; ends time; week date;
                BEGIN
                  FOR item IN SELECT value FROM jsonb_array_elements(NEW."ScheduleJson") ORDER BY value->>'Start' LOOP
                    IF jsonb_typeof(item) <> 'object' OR
                       COALESCE(item->>'Start', '') !~ '^([01][0-9]|2[0-3]):[0-5][0-9]:00$' OR
                       COALESCE(item->>'End', '') !~ '^([01][0-9]|2[0-3]):[0-5][0-9]:00$' OR
                       jsonb_typeof(item->'Activity') IS DISTINCT FROM 'string' OR
                       length(btrim(COALESCE(item->>'Activity', ''))) NOT BETWEEN 1 AND 200 THEN
                      RAISE EXCEPTION 'Invalid classroom schedule' USING ERRCODE='23514', CONSTRAINT='CK_Classrooms_Schedule';
                    END IF;
                    starts := (item->>'Start')::time; ends := (item->>'End')::time;
                    IF starts >= ends OR starts < previous_end THEN
                      RAISE EXCEPTION 'Classroom activities overlap' USING ERRCODE='23514', CONSTRAINT='CK_Classrooms_Schedule';
                    END IF;
                    previous_end := ends;
                  END LOOP;
                  FOR item IN SELECT value FROM jsonb_array_elements(NEW."WeeklyNotesJson") LOOP
                    IF jsonb_typeof(item) <> 'object' OR
                       COALESCE(item->>'WeekStart', '') !~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}$' OR
                       jsonb_typeof(item->'Text') IS DISTINCT FROM 'string' OR
                       length(btrim(COALESCE(item->>'Text', ''))) NOT BETWEEN 1 AND 2000 THEN
                      RAISE EXCEPTION 'Invalid classroom note' USING ERRCODE='23514', CONSTRAINT='CK_Classrooms_WeeklyNotes';
                    END IF;
                    BEGIN
                      week := (item->>'WeekStart')::date;
                    EXCEPTION WHEN datetime_field_overflow OR invalid_datetime_format THEN
                      RAISE EXCEPTION 'Invalid week date' USING ERRCODE='23514', CONSTRAINT='CK_Classrooms_WeeklyNotes';
                    END;
                    IF extract(isodow FROM week) <> 1 THEN
                      RAISE EXCEPTION 'Week must begin Monday' USING ERRCODE='23514', CONSTRAINT='CK_Classrooms_WeeklyNotes';
                    END IF;
                  END LOOP;
                  IF (SELECT count(*) <> count(DISTINCT value->>'WeekStart') FROM jsonb_array_elements(NEW."WeeklyNotesJson")) THEN
                    RAISE EXCEPTION 'Duplicate week' USING ERRCODE='23514', CONSTRAINT='CK_Classrooms_WeeklyNotes';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_Classroom_Details" BEFORE INSERT OR UPDATE ON "Classrooms"
                  FOR EACH ROW EXECUTE FUNCTION guard_classroom_details();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Classroom details are forward-only; use a reviewed corrective migration.");
        }
    }
}
