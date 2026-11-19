using Microsoft.EntityFrameworkCore;
using Predict.Backend.Models;

namespace Predict.Backend.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<PredictDiscoveryOpportunity> PredictDiscoveryOpportunities { get; set; }
        public DbSet<PredictDiscoveryProcess> PredictDiscoveryProcesses { get; set; }
        public DbSet<PredictDiscoveryTask> PredictDiscoveryTasks { get; set; }
        public DbSet<PredictDiscoveryPainPoint> PredictDiscoveryPainPoints { get; set; }
        public DbSet<ActionPlanNarrative> ActionPlanNarratives { get; set; }
        public DbSet<ActionPlan> ActionPlans { get; set; }
        public DbSet<PredictAuditHistory> AuditHistories { get; set; }
        public DbSet<PredictDecisionLog> DecisionLogs { get; set; }
        public DbSet<IntegrationEntity> IntegrationEntities { get; set; }
        public DbSet<PredictAttAssignmentRule> AssignmentRules { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ActionPlanNarrative>(entity =>
            {
                entity.ToTable("predict_narrativas");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.IdPlano).HasColumnName("id_plano").IsRequired();
                entity.Property(e => e.Problema).HasColumnName("problema").IsRequired();
                entity.Property(e => e.Impacto).HasColumnName("impacto").IsRequired();
                entity.Property(e => e.Acoes).HasColumnName("acoes").IsRequired();
                entity.Property(e => e.Urgencia).HasColumnName("urgencia").IsRequired().HasMaxLength(10);
                entity.Property(e => e.JustificativaUrgencia).HasColumnName("justificativa_urgencia").IsRequired();
                entity.Property(e => e.ModeloUtilizado).HasColumnName("modelo_utilizado").IsRequired().HasMaxLength(50);
                entity.Property(e => e.DtGeracao).HasColumnName("dt_geracao").IsRequired();
                entity.Property(e => e.Status).HasColumnName("status").IsRequired().HasMaxLength(20);
                entity.Property(e => e.Erro).HasColumnName("erro");

                entity.HasIndex(e => e.IdPlano).IsUnique();
            });

            modelBuilder.Entity<PredictDiscoveryOpportunity>(entity =>
            {
                entity.ToTable("predict_discovery_opportunities");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("opportunity_id");
                entity.Property(e => e.OpportunityCode).HasColumnName("opportunity_code").IsRequired().HasMaxLength(50);
                entity.Property(e => e.ClientCode).HasColumnName("client_code").IsRequired().HasMaxLength(50);
                entity.Property(e => e.Title).HasColumnName("title").IsRequired().HasMaxLength(255);
                entity.Property(e => e.ProcessId).HasColumnName("process_id");
                entity.Property(e => e.OpportunityCategory).HasColumnName("opportunity_category");
                entity.Property(e => e.OperationalArea).HasColumnName("operational_area");
                entity.Property(e => e.OriginModule).HasColumnName("origin_module");
                entity.Property(e => e.Summary).HasColumnName("summary");
                entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
                entity.Property(e => e.OwnerName).HasColumnName("owner_name");
                entity.Property(e => e.SourceReference).HasColumnName("source_reference");
                entity.Property(e => e.OriginType).HasColumnName("origin_type");
                entity.Property(e => e.RadarPatternReference).HasColumnName("radar_pattern_reference");
                entity.Property(e => e.ExpectedGainClassification).HasColumnName("expected_gain_classification");
                entity.Property(e => e.ImplementationEffortClassification).HasColumnName("implementation_effort_classification");
                entity.Property(e => e.OperationalRiskClassification).HasColumnName("operational_risk_classification");
                entity.Property(e => e.PriorityScore).HasColumnName("priority_score");
                entity.Property(e => e.PriorityRank).HasColumnName("priority_rank");
                entity.Property(e => e.EstimatedVolume).HasColumnName("estimated_volume");
                entity.Property(e => e.VolumeUnit).HasColumnName("volume_unit");
                entity.Property(e => e.FrequencyValue).HasColumnName("frequency_value");
                entity.Property(e => e.FrequencyUnit).HasColumnName("frequency_unit");
                entity.Property(e => e.EstimatedTimeSavedMinutes).HasColumnName("estimated_time_saved_minutes");
                entity.Property(e => e.EstimatedCostAvoidance).HasColumnName("estimated_cost_avoidance");
                entity.Property(e => e.AutomationPotentialPercent).HasColumnName("automation_potential_percent");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.DiscardReason).HasColumnName("discard_reason");
                entity.Property(e => e.BuilderReference).HasColumnName("builder_reference");
                entity.Property(e => e.ConvertedToBuilder).HasColumnName("converted_to_builder");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.CreatedBy).HasColumnName("created_by");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
                entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
                // Catálogo semântico
                entity.Property(e => e.CatalogId).HasColumnName("catalog_id");
                entity.Property(e => e.CatalogScore).HasColumnName("catalog_score");
                entity.Property(e => e.CatalogMatchedAt).HasColumnName("catalog_matched_at");
                
                entity.HasIndex(e => new { e.ClientCode, e.OpportunityCode }).IsUnique();

                entity.HasOne(e => e.Process)
                      .WithMany()
                      .HasForeignKey(e => e.ProcessId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<PredictDiscoveryProcess>(entity =>
            {
                entity.ToTable("predict_discovery_processes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("process_id");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
            });

            modelBuilder.Entity<PredictDiscoveryTask>(entity =>
            {
                entity.ToTable("predict_discovery_tasks");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("task_id");
                entity.Property(e => e.OpportunityId).HasColumnName("opportunity_id");
                entity.Property(e => e.TaskCode).HasColumnName("task_code").IsRequired().HasMaxLength(50);
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(150);
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.Sequence).HasColumnName("sequence");
                entity.Property(e => e.EstimatedVolume).HasColumnName("estimated_volume");
                entity.Property(e => e.VolumeUnit).HasColumnName("volume_unit");
                entity.Property(e => e.FrequencyValue).HasColumnName("frequency_value");
                entity.Property(e => e.FrequencyUnit).HasColumnName("frequency_unit");
                entity.Property(e => e.AverageTimeInMinutes).HasColumnName("average_execution_minutes");
                entity.Property(e => e.AutomationCandidate).HasColumnName("automation_candidate");
                entity.Property(e => e.Status).HasColumnName("status");
            });

            modelBuilder.Entity<PredictDiscoveryPainPoint>(entity =>
            {
                entity.ToTable("predict_discovery_pain_points");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("pain_point_id");
                entity.Property(e => e.OpportunityId).HasColumnName("opportunity_id");
                entity.Property(e => e.PainPointCode).HasColumnName("pain_point_code").IsRequired().HasMaxLength(50);
                entity.Property(e => e.Title).HasColumnName("title").IsRequired().HasMaxLength(150);
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.ImpactType).HasColumnName("impact_type");
                entity.Property(e => e.ImpactLevel).HasColumnName("impact_level");
                entity.Property(e => e.Evidence).HasColumnName("evidence");
                entity.Property(e => e.RecurrenceNotes).HasColumnName("recurrence_notes");
                entity.Property(e => e.Status).HasColumnName("status");
            });



            modelBuilder.Entity<PredictAuditHistory>(entity =>
            {
                entity.ToTable("predict_audit_history");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<PredictDecisionLog>(entity =>
            {
                entity.ToTable("predict_decision_log");
                entity.HasKey(e => e.Id);
            });

            // View mapping — keyless, read-only
            modelBuilder.Entity<ActionPlan>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("vw_predict_plano_acao_hostname");
            });

            modelBuilder.Entity<IntegrationEntity>(entity =>
            {
                entity.ToTable("integration_entities");
                entity.HasKey(e => e.EntitiesId);
            });

            modelBuilder.Entity<PredictAttAssignmentRule>(entity =>
            {
                entity.ToTable("predict_att_assignment_rules");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Level, e.Priority }).IsUnique();
            });
        }
    }
}
