using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'DiagramRequests'
                          AND column_name = 'ReportUrl'
                    ) THEN
                        ALTER TABLE "DiagramRequests" ADD COLUMN "ReportUrl" character varying(2048);
                    ELSE
                        ALTER TABLE "DiagramRequests" ALTER COLUMN "ReportUrl" TYPE character varying(2048);
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'DiagramRequests'
                          AND column_name = 'ErrorMessage'
                    ) THEN
                        ALTER TABLE "DiagramRequests" ADD COLUMN "ErrorMessage" character varying(1000);
                    ELSE
                        ALTER TABLE "DiagramRequests" ALTER COLUMN "ErrorMessage" TYPE character varying(1000);
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ReportUrl",
                table: "DiagramRequests",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "DiagramRequests",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
