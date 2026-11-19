using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Predict.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditAndDecisionLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "predict_discovery_opportunity_history");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "predict_discovery_tasks",
                newName: "task_id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "predict_discovery_processes",
                newName: "process_id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "predict_discovery_pain_points",
                newName: "pain_point_id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "predict_discovery_opportunities",
                newName: "opportunity_id");

            migrationBuilder.AddColumn<string>(
                name: "origin_module",
                table: "predict_discovery_opportunities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "predict_audit_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    module_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    field_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    justification = table.Column<string>(type: "text", nullable: true),
                    changed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_predict_audit_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "predict_decision_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    module_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    decision_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rationale = table.Column<string>(type: "text", nullable: false),
                    context_payload = table.Column<string>(type: "jsonb", nullable: true),
                    decided_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_predict_decision_log", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "predict_audit_history");

            migrationBuilder.DropTable(
                name: "predict_decision_log");

            migrationBuilder.DropColumn(
                name: "origin_module",
                table: "predict_discovery_opportunities");

            migrationBuilder.RenameColumn(
                name: "task_id",
                table: "predict_discovery_tasks",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "process_id",
                table: "predict_discovery_processes",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "pain_point_id",
                table: "predict_discovery_pain_points",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "opportunity_id",
                table: "predict_discovery_opportunities",
                newName: "id");

            migrationBuilder.CreateTable(
                name: "predict_discovery_opportunity_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    changed_by = table.Column<string>(type: "text", nullable: true),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    field_changed = table.Column<string>(type: "text", nullable: true),
                    justification = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    opportunity_id = table.Column<int>(type: "integer", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_predict_discovery_opportunity_history", x => x.id);
                });
        }
    }
}
