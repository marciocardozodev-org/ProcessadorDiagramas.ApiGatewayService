using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadedFileMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "DiagramRequests",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "DiagramRequests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "DiagramRequests",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "DiagramRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "DiagramRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoragePath",
                table: "DiagramRequests",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "DiagramRequests");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "DiagramRequests");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "DiagramRequests");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "DiagramRequests");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "DiagramRequests");

            migrationBuilder.DropColumn(
                name: "StoragePath",
                table: "DiagramRequests");
        }
    }
}
