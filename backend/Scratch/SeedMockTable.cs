using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Predict.Backend.Infrastructure.Data;

namespace Predict.Backend.Scratch
{
    public class SeedMockTable
    {
        public static async System.Threading.Tasks.Task Run(IServiceProvider services)
        {
            var context = services.GetRequiredService<AppDbContext>();
            Console.WriteLine("--- Seeding Mock Table & Narrativas for DEV ---");

            // Try to drop view
            try { await context.Database.ExecuteSqlRawAsync("DROP VIEW IF EXISTS vw_predict_plano_acao_hostname CASCADE;"); } catch {}
            // Try to drop table
            try { await context.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS vw_predict_plano_acao_hostname CASCADE;"); } catch {}
            // Try to drop narrativas table to ensure schema sync
            try { await context.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS predict_narrativas CASCADE;"); } catch {}

            var sql = @"
                CREATE TABLE IF NOT EXISTS vw_predict_plano_acao_hostname (
                    id_plano SERIAL PRIMARY KEY,
                    id_contrato INT,
                    nome_entidade TEXT,
                    ranking_hostname INT,
                    nm_host TEXT,
                    nm_completo TEXT,
                    descricao_chave TEXT,
                    description_exemplo TEXT,
                    rotulo_acao TEXT,
                    tx_acao_inicial_suger TEXT,
                    prioridade_sugerida TEXT,
                    justificativa_resumida TEXT,
                    titulo_melhoria TEXT,
                    score_hostname NUMERIC,
                    qt_eventos INT,
                    pct_volume_contrato NUMERIC,
                    dias_com_evento INT,
                    qt_disaster INT,
                    qt_high INT,
                    qt_average INT,
                    qt_warning INT,
                    pct_graves NUMERIC,
                    pct_nao_graves NUMERIC,
                    ultima_ocorrencia TIMESTAMP WITH TIME ZONE,
                    dt_inicio_analise TIMESTAMP WITH TIME ZONE,
                    dt_fim_analise TIMESTAMP WITH TIME ZONE,
                    dt_registro_sistema TIMESTAMP WITH TIME ZONE,
                    nu_requisicao_itsm INT,
                    cd_agente TEXT,
                    cd_categoria TEXT,
                    nu_evm BIGINT
                );

                TRUNCATE TABLE vw_predict_plano_acao_hostname CASCADE;

                INSERT INTO vw_predict_plano_acao_hostname 
                (id_plano, id_contrato, nome_entidade, ranking_hostname, nm_host, rotulo_acao, prioridade_sugerida, titulo_melhoria, score_hostname, qt_eventos, dt_registro_sistema)
                VALUES 
                (1, 101, 'Cliente Alpha', 1, 'SRV-DB-01', 'OTIMIZACAO', 'P2', 'Otimização de Query', 95.5, 1500, NOW()),
                (2, 101, 'Cliente Alpha', 2, 'SRV-WEB-01', 'CAPACIDADE', 'P3', 'Upgrade de Memória', 82.0, 800, NOW() - INTERVAL '1 day'),
                (3, 102, 'Cliente Beta', 1, 'APP-01', 'DISPONIBILIDADE', 'P2', 'Migração de Storage', 88.5, 1200, NOW()),
                (4, 103, 'Service IT', 1, 'MNG-01', 'DISPONIBILIDADE', 'P2', 'Manutenção Preventiva', 99.0, 200, NOW());

                CREATE TABLE IF NOT EXISTS predict_narrativas (
                    id SERIAL PRIMARY KEY,
                    id_plano INT NOT NULL,
                    problema TEXT NOT NULL,
                    impacto TEXT NOT NULL,
                    acoes JSONB NOT NULL,
                    urgencia VARCHAR(10) NOT NULL,
                    justificativa_urgencia TEXT NOT NULL,
                    modelo_utilizado VARCHAR(50) NOT NULL,
                    dt_geracao TIMESTAMP WITH TIME ZONE NOT NULL,
                    status VARCHAR(20) NOT NULL,
                    erro TEXT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_predict_narrativas_id_plano ON predict_narrativas (id_plano);

                TRUNCATE TABLE predict_narrativas;

                INSERT INTO predict_narrativas 
                (id_plano, problema, impacto, acoes, urgencia, justificativa_urgencia, modelo_utilizado, dt_geracao, status)
                VALUES 
                (1, 
                 'O servidor SRV-DB-01 está apresentando uma degradação crítica de performance nas consultas SQL devido à falta de índices otimizados.',
                 'Operações de final de mês do Cliente Alpha podem sofrer atrasos de até 4 horas, impactando o faturamento.',
                 '[""Analisar o plano de execução das top 5 queries mais lentas"",""Criar índices recomendados pelo Database Engine Advisor"",""Realizar a limpeza de logs temporários do sistema"",""Monitorar a latência de disco no período de pico"",""Gerar relatório de ganho de performance após 24h""]',
                 'ALTA',
                 'O volume de eventos graves representa 95% do total do host.',
                 'claude-haiku-3.5',
                 NOW(),
                 'gerado'
                );
            ";

            try {
                await context.Database.ExecuteSqlRawAsync(sql);
                Console.WriteLine("Mock table and Narrativas created and seeded successfully.");
            } catch (Exception ex) {
                Console.WriteLine($"Error seeding mock data: {ex.Message}");
            }
        }
    }
}
