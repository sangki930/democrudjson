using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace DemoCrud.Api.Data.Migrations;

public partial class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Adopt the existing demo tables, or create them in a provisioned empty schema.
        migrationBuilder.Sql("""
        CREATE TABLE IF NOT EXISTS democrud_demo.items (
        id uuid PRIMARY KEY,
        name varchar(200) NOT NULL CHECK (length(trim(name)) > 0),
        description varchar(2000),
        is_completed boolean NOT NULL DEFAULT false,
        created_at timestamptz NOT NULL DEFAULT now(),
        updated_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS democrud_demo."LAYER" (
        id varchar(24) PRIMARY KEY CHECK (length(trim(id)) > 0),
        data jsonb NOT NULL CHECK (jsonb_typeof(data) = 'object')
        );
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("The initial baseline adopts existing tables and cannot be rolled back automatically.");
}
