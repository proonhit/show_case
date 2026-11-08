using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Showcase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SmallStorageKey",
                table: "ModelImages",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcessingJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Steps = table.Column<string>(type: "jsonb", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingJobs_ModelFiles_ModelFileId",
                        column: x => x.ModelFileId,
                        principalTable: "ModelFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessingJobs_Models_ModelId",
                        column: x => x.ModelId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingJobs_ModelFileId",
                table: "ProcessingJobs",
                column: "ModelFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingJobs_ModelId_CreatedAt",
                table: "ProcessingJobs",
                columns: new[] { "ModelId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessingJobs");

            migrationBuilder.DropColumn(
                name: "SmallStorageKey",
                table: "ModelImages");
        }
    }
}
