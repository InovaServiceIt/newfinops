using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Predict.Backend.Infrastructure.Data;

namespace Predict.Backend.Scratch
{
    public static class FixTables
    {
        public static async Task Run(IServiceProvider serviceProvider)
        {
            var db = serviceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE EXTENSION IF NOT EXISTS vector;

                CREATE TABLE IF NOT EXISTS predict_controle_execucao (
                  id               SERIAL PRIMARY KEY,
                  tp_rotina        VARCHAR(50) NOT NULL DEFAULT 'ANALISE_PADROES',
                  dt_execucao      TIMESTAMP NOT NULL DEFAULT NOW(),
                  status           VARCHAR(20) NOT NULL,
                  qt_alertas       INTEGER,
                  qt_clusters      INTEGER,
                  qt_sugestoes     INTEGER,
                  executado_por    VARCHAR(100),
                  erro             TEXT,
                  forcado          BOOLEAN DEFAULT FALSE
                );

                CREATE INDEX IF NOT EXISTS idx_controle_tp_rotina_dt 
                  ON predict_controle_execucao(tp_rotina, dt_execucao DESC);

                DROP TABLE IF EXISTS predict_alert_embeddings CASCADE;

                CREATE TABLE predict_alert_embeddings (
                  id               SERIAL PRIMARY KEY,
                  alert_id         INTEGER NOT NULL,
                  id_contrato      VARCHAR NOT NULL,
                  description_flat TEXT NOT NULL,
                  embedding        vector(1024),
                  dt_geracao       TIMESTAMP DEFAULT NOW(),
                  UNIQUE(alert_id)
                );

                CREATE INDEX IF NOT EXISTS idx_embeddings_alert_id ON predict_alert_embeddings(alert_id);
                CREATE INDEX IF NOT EXISTS idx_embeddings_contrato ON predict_alert_embeddings(id_contrato);


                CREATE TABLE IF NOT EXISTS predict_alert_clusters (
                  id                   SERIAL PRIMARY KEY,
                  id_contrato          VARCHAR NOT NULL,
                  nm_host              TEXT,
                  descricao_cluster    TEXT NOT NULL,
                  qt_alertas           INTEGER NOT NULL,
                  similaridade_media   NUMERIC(5,4),
                  dt_primeiro_alerta   TIMESTAMP,
                  dt_ultimo_alerta     TIMESTAMP,
                  tem_regra            BOOLEAN DEFAULT FALSE,
                  id_execucao          INTEGER REFERENCES predict_controle_execucao(id),
                  dt_geracao           TIMESTAMP DEFAULT NOW()
                );

                CREATE TABLE IF NOT EXISTS predict_sugestao_regra (
                  id                       SERIAL PRIMARY KEY,
                  id_contrato              VARCHAR NOT NULL,
                  nm_host                  TEXT,
                  qt_alertas_cluster       INTEGER,
                  descricao_representativa TEXT,
                  id_cluster               INTEGER REFERENCES predict_alert_clusters(id),

                  tp_alvo                  VARCHAR(30),
                  tp_operador              VARCHAR(10),
                  tx_padrao                TEXT,
                  rotulo_acao              VARCHAR(60),
                  cd_prioridade            VARCHAR(5),
                  tx_acao_inicial_suger    TEXT,
                  justificativa_ia         TEXT,

                  status        VARCHAR(30) DEFAULT 'AGUARDANDO_APROVACAO',
                  aprovado_por  VARCHAR(100),
                  dt_aprovacao  TIMESTAMP,
                  observacao    TEXT,
                  id_execucao   INTEGER REFERENCES predict_controle_execucao(id),
                  dt_sugestao   TIMESTAMP DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS idx_sugestao_status ON predict_sugestao_regra(status);
                CREATE INDEX IF NOT EXISTS idx_sugestao_contrato ON predict_sugestao_regra(id_contrato);
            ");
            Console.WriteLine("Tables fixed.");
        }
    }
}
