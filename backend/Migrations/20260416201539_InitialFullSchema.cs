using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Predict.Backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialFullSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "predict_discovery_opportunity_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    opportunity_id = table.Column<int>(type: "integer", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    field_changed = table.Column<string>(type: "text", nullable: true),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    justification = table.Column<string>(type: "text", nullable: true),
                    changed_by = table.Column<string>(type: "text", nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_predict_discovery_opportunity_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "predict_discovery_pain_points",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    opportunity_id = table.Column<int>(type: "integer", nullable: false),
                    pain_point_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    impact_type = table.Column<string>(type: "text", nullable: true),
                    impact_level = table.Column<string>(type: "text", nullable: true),
                    evidence = table.Column<string>(type: "text", nullable: true),
                    recurrence_notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_predict_discovery_pain_points", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "predict_discovery_processes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_predict_discovery_processes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "predict_discovery_tasks",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    opportunity_id = table.Column<int>(type: "integer", nullable: false),
                    task_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    estimated_volume = table.Column<decimal>(type: "numeric", nullable: true),
                    volume_unit = table.Column<string>(type: "text", nullable: true),
                    frequency_value = table.Column<decimal>(type: "numeric", nullable: true),
                    frequency_unit = table.Column<string>(type: "text", nullable: true),
                    average_execution_minutes = table.Column<int>(type: "integer", nullable: true),
                    automation_candidate = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_predict_discovery_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "predict_narrativas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_plano = table.Column<int>(type: "integer", nullable: false),
                    problema = table.Column<string>(type: "text", nullable: false),
                    impacto = table.Column<string>(type: "text", nullable: false),
                    acoes = table.Column<List<string>>(type: "jsonb", nullable: false),
                    urgencia = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    justificativa_urgencia = table.Column<string>(type: "text", nullable: false),
                    modelo_utilizado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    dt_geracao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    erro = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_predict_narrativas", x => x.id);
                });


            migrationBuilder.CreateTable(
                name: "predict_discovery_opportunities",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    client_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    opportunity_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    process_id = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    opportunity_category = table.Column<string>(type: "text", nullable: true),
                    operational_area = table.Column<string>(type: "text", nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    requested_by = table.Column<string>(type: "text", nullable: true),
                    owner_name = table.Column<string>(type: "text", nullable: true),
                    source_reference = table.Column<string>(type: "text", nullable: true),
                    origin_type = table.Column<string>(type: "text", nullable: true),
                    radar_pattern_reference = table.Column<string>(type: "text", nullable: true),
                    expected_gain_classification = table.Column<string>(type: "text", nullable: true),
                    implementation_effort_classification = table.Column<string>(type: "text", nullable: true),
                    operational_risk_classification = table.Column<string>(type: "text", nullable: true),
                    priority_score = table.Column<double>(type: "double precision", nullable: false),
                    priority_rank = table.Column<int>(type: "integer", nullable: false),
                    estimated_volume = table.Column<decimal>(type: "numeric", nullable: true),
                    volume_unit = table.Column<string>(type: "text", nullable: true),
                    frequency_value = table.Column<decimal>(type: "numeric", nullable: true),
                    frequency_unit = table.Column<string>(type: "text", nullable: true),
                    estimated_time_saved_minutes = table.Column<int>(type: "integer", nullable: true),
                    estimated_cost_avoidance = table.Column<decimal>(type: "numeric", nullable: true),
                    automation_potential_percent = table.Column<double>(type: "double precision", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    discard_reason = table.Column<string>(type: "text", nullable: true),
                    builder_reference = table.Column<string>(type: "text", nullable: true),
                    converted_to_builder = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_predict_discovery_opportunities", x => x.id);
                    table.ForeignKey(
                        name: "FK_predict_discovery_opportunities_predict_discovery_processes~",
                        column: x => x.process_id,
                        principalTable: "predict_discovery_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_predict_discovery_opportunities_client_code_opportunity_code",
                table: "predict_discovery_opportunities",
                columns: new[] { "client_code", "opportunity_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_predict_discovery_opportunities_process_id",
                table: "predict_discovery_opportunities",
                column: "process_id");

            migrationBuilder.CreateIndex(
                name: "IX_predict_narrativas_id_plano",
                table: "predict_narrativas",
                column: "id_plano",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "predict_discovery_opportunities");

            migrationBuilder.DropTable(
                name: "predict_discovery_opportunity_history");

            migrationBuilder.DropTable(
                name: "predict_discovery_pain_points");

            migrationBuilder.DropTable(
                name: "predict_discovery_tasks");

            migrationBuilder.DropTable(
                name: "predict_narrativas");


            migrationBuilder.DropTable(
                name: "predict_discovery_processes");
        }
    }
}
