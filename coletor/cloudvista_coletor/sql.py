"""SQL do coletor (psycopg 3, parâmetros nomeados; nada de concatenação, nada de SELECT *)."""

# Pega 1 job PENDENTE (mais urgente primeiro) sem disputar com outras instâncias.
CLAIM = """
UPDATE job_execucao
SET status = 'EXECUTANDO', iniciado_em = now(), heartbeat_em = now(),
    executor = %(executor)s, tentativas = tentativas + 1
WHERE id = (SELECT id FROM job_execucao
            WHERE status = 'PENDENTE'
            ORDER BY prioridade, criado_em
            FOR UPDATE SKIP LOCKED
            LIMIT 1)
RETURNING id, tipo_job, cliente_id, cloud_conta_id, escopo
"""

HEARTBEAT = "UPDATE job_execucao SET heartbeat_em = now() WHERE id = %(id)s AND status = 'EXECUTANDO'"

FINALIZAR = """
UPDATE job_execucao
SET status = %(status)s,
    finalizado_em = now(),
    coletado_em = CASE WHEN %(coletado)s THEN now() ELSE coletado_em END,
    heartbeat_em = now(),
    erro = %(erro)s,
    resumo = %(resumo)s::jsonb,
    coletor_versao = %(versao)s
WHERE id = %(id)s AND status = 'EXECUTANDO'
"""

# SIGTERM antes de começar a coletar: devolve o job sem consumir a tentativa.
DEVOLVER = """
UPDATE job_execucao
SET status = 'PENDENTE', executor = NULL, iniciado_em = NULL, heartbeat_em = NULL,
    tentativas = GREATEST(tentativas - 1, 0)
WHERE id = %(id)s AND status = 'EXECUTANDO'
"""

PARAMETRO = "SELECT valor FROM parametro WHERE chave = %(chave)s"

CREDENCIAL = """
SELECT cr.id, cr.tipo, cr.versao_formato, cr.status, cr.conteudo_cifrado,
       cd.versao_kek, cd.dek_cifrada, cc.cliente_id, cc.provedor, cc.identificador
FROM credencial cr
JOIN chave_dados cd ON cd.id = cr.chave_dados_id
JOIN cloud_conta cc ON cc.id = cr.cloud_conta_id
WHERE cr.cloud_conta_id = %(conta)s
"""

NOME_CLIENTE = "SELECT nome FROM cliente WHERE id = %(cliente)s"

RESULTADO_TESTE = """
UPDATE credencial
SET status = %(status)s, testada_em = now(), teste_resultado = %(resultado)s
WHERE id = %(credencial)s
"""

AUDITORIA = """
INSERT INTO credencial_auditoria (cloud_conta_id, credencial_id, acao, detalhe, usuario_id, origem)
VALUES (%(conta)s, %(credencial)s, %(acao)s, %(detalhe)s, NULL, 'COLETOR')
"""

COLETA_BRUTA = """
INSERT INTO coleta_bruta (job_execucao_id, formato, tamanho_bytes, sha256, conteudo)
VALUES (%(job)s, 'JSON_GZIP', %(tamanho)s, %(sha256)s, %(conteudo)s)
"""
