"""Loop do cloudvista-collector: fila job_execucao → decifra → testa/coleta → grava resultado."""
from __future__ import annotations

import json
import logging
import os
import signal
import socket
import sys
import threading
import time
from dataclasses import dataclass

from . import VERSAO, regras, sql
from .cripto import ChavesMestras, CofreErro, aad_credencial, aad_dek, decifrar_v1, zerar
from .seguranca import LIMITE_ERRO, LIMITE_TESTE, FiltroSanitizador, descrever_excecao, sanitizar
from .testes_credencial import executar_teste

log = logging.getLogger("cloudvista.coletor")


class JobFalhou(Exception):
    """Falha esperada do job: a mensagem já é segura para gravar em `erro`."""


@dataclass
class Job:
    id: int
    tipo_job: str
    cliente_id: int
    cloud_conta_id: int
    escopo: dict


@dataclass
class Credencial:
    id: int
    tipo: str
    provedor: str
    identificador: str
    dados: dict  # JSON decifrado: fica só em memória, durante o job


def configurar_logging() -> None:
    manipulador = logging.StreamHandler(sys.stdout)
    manipulador.setFormatter(logging.Formatter("%(asctime)s %(levelname)s %(name)s: %(message)s", "%Y-%m-%d %H:%M:%S"))
    manipulador.addFilter(FiltroSanitizador())
    raiz = logging.getLogger()
    raiz.handlers[:] = [manipulador]
    raiz.setLevel(logging.INFO)
    for ruidoso in ("oci", "urllib3", "botocore", "boto3", "azure", "google", "psycopg"):
        logging.getLogger(ruidoso).setLevel(logging.WARNING)


class Servico:
    def __init__(self, url_banco: str, chaves: ChavesMestras):
        self.url_banco = url_banco
        self.chaves = chaves
        self.executor = socket.gethostname()[:100]
        self.parar = threading.Event()
        self.versao = f"cv-{VERSAO}/oci-desconhecida"

    # ── Conexão ─────────────────────────────────────────────────────────────
    def conectar(self):
        import psycopg  # noqa: PLC0415

        return psycopg.connect(self.url_banco, autocommit=True, application_name="CloudVista-Collector", connect_timeout=10)

    def parametro(self, conn, chave: str) -> int:
        try:
            linha = conn.execute(sql.PARAMETRO, {"chave": chave}).fetchone()
        except Exception:  # noqa: BLE001
            linha = None
        return regras.parametro_inteiro(chave, linha[0] if linha else None)

    # ── Loop ────────────────────────────────────────────────────────────────
    def executar(self) -> int:
        from .adaptadores.oci import versao_coletor_oci  # noqa: PLC0415

        self.versao = f"cv-{VERSAO}/oci-{versao_coletor_oci()}"
        signal.signal(signal.SIGTERM, self._sinal)
        signal.signal(signal.SIGINT, self._sinal)
        log.info("cloudvista-collector %s iniciado (executor %s).", self.versao, self.executor)

        conn = None
        while not self.parar.is_set():
            try:
                if conn is None or conn.closed:
                    conn = self.conectar()
                job = self.pegar_job(conn)
                if job is None:
                    self.parar.wait(self.parametro(conn, "coletor.intervalo_poll_seg"))
                    continue
                self.processar(conn, job)
            except Exception as exc:  # noqa: BLE001 - nunca derruba o loop por uma falha de banco/rede
                log.error("Falha no loop do coletor: %s", type(exc).__name__)
                if conn is not None:
                    try:
                        conn.close()
                    except Exception:  # noqa: BLE001
                        pass
                conn = None
                self.parar.wait(10)
        log.info("cloudvista-collector encerrado.")
        return 0

    def _sinal(self, numero, _frame) -> None:
        log.info("Sinal %s recebido: termina o job atual e sai.", numero)
        self.parar.set()

    def pegar_job(self, conn) -> Job | None:
        linha = conn.execute(sql.CLAIM, {"executor": self.executor}).fetchone()
        if not linha:
            return None
        escopo = linha[4] if isinstance(linha[4], dict) else json.loads(linha[4] or "{}")
        return Job(id=linha[0], tipo_job=linha[1], cliente_id=linha[2], cloud_conta_id=linha[3], escopo=escopo)

    # ── Processamento ───────────────────────────────────────────────────────
    def processar(self, conn, job: Job) -> None:
        log.info("Job %s (%s) da conta %s iniciado.", job.id, job.tipo_job, job.cloud_conta_id)
        batimento = Batimento(self, job.id, self.parametro(conn, "coletor.heartbeat_seg"))
        batimento.start()
        inicio = time.monotonic()
        credencial = None
        try:
            credencial = self.carregar_credencial(conn, job)

            # SIGTERM antes de começar a testar/coletar: devolve o job para a fila.
            if self.parar.is_set():
                conn.execute(sql.DEVOLVER, {"id": job.id})
                log.info("Job %s devolvido à fila (encerramento antes de iniciar).", job.id)
                return

            if job.tipo_job == "TESTE_CREDENCIAL":
                self.testar(conn, job, credencial)
            elif job.tipo_job in regras.TIPOS_COLETA:
                self.coletar(conn, job, credencial, inicio)
            else:
                raise JobFalhou(f"Tipo de job não suportado pelo coletor: {job.tipo_job}")
        except JobFalhou as exc:
            self.finalizar(conn, job.id, "ERRO", erro=str(exc), resumo={"motivo": str(exc)})
        except Exception as exc:  # noqa: BLE001
            descricao = descrever_excecao(exc, LIMITE_ERRO)
            log.error("Job %s falhou: %s", job.id, descricao)
            self.finalizar(conn, job.id, "ERRO", erro=descricao, resumo={"falha": type(exc).__name__})
        finally:
            batimento.parar()
            if credencial is not None:
                credencial.dados.clear()

    def carregar_credencial(self, conn, job: Job) -> Credencial:
        linha = conn.execute(sql.CREDENCIAL, {"conta": job.cloud_conta_id}).fetchone()
        if not linha or linha[3] == "DESATIVADA":
            raise JobFalhou(regras.MENSAGEM_SEM_CREDENCIAL)
        (cred_id, tipo, versao_formato, _status, conteudo, versao_kek, dek_cifrada,
         cliente_id, provedor, identificador) = linha
        if versao_formato != 1:
            raise JobFalhou("Formato da credencial não suportado")

        dek = claro = None
        try:
            dek = decifrar_v1(self.chaves.kek(versao_kek), dek_cifrada, aad_dek(cliente_id, versao_kek))
            claro = decifrar_v1(dek, conteudo, aad_credencial(job.cloud_conta_id, tipo))
            dados = json.loads(claro.decode("utf-8"))
        except CofreErro as exc:
            raise JobFalhou(f"Não foi possível decifrar a credencial: {exc}") from None
        except ValueError:
            raise JobFalhou("Credencial decifrada com conteúdo inválido") from None
        finally:
            zerar(dek)
            zerar(claro)
        return Credencial(id=cred_id, tipo=tipo, provedor=provedor, identificador=identificador, dados=dados)

    def testar(self, conn, job: Job, cred: Credencial) -> None:
        timeout = self.parametro(conn, "coletor.timeout_teste_seg")
        ok, mensagem = executar_teste(cred.tipo, cred.dados, timeout)
        mensagem = sanitizar(mensagem, LIMITE_TESTE)
        with conn.transaction():
            conn.execute(sql.RESULTADO_TESTE, {
                "status": "ATIVA" if ok else "FALHA_TESTE", "resultado": mensagem, "credencial": cred.id})
            conn.execute(sql.AUDITORIA, {
                "conta": job.cloud_conta_id, "credencial": cred.id,
                "acao": "TESTADA_OK" if ok else "TESTADA_FALHA", "detalhe": mensagem})
            self.finalizar(conn, job.id, "CONCLUIDO" if ok else "ERRO",
                           erro=None if ok else mensagem,
                           resumo={"tipo": cred.tipo, "resultado": mensagem, "ok": ok})
        log.info("Job %s: teste de credencial %s.", job.id, "OK" if ok else "falhou")

    def coletar(self, conn, job: Job, cred: Credencial, inicio: float) -> None:
        if cred.provedor not in regras.PROVEDORES_COM_COLETA:
            raise JobFalhou(regras.MENSAGEM_SEM_COLETA)

        from .adaptadores import oci as adaptador_oci  # noqa: PLC0415

        nome_cliente = self.nome_cliente(conn, job.cliente_id)
        try:
            config = adaptador_oci.montar_config(cred.dados)
        except ValueError as exc:
            raise JobFalhou(sanitizar(exc, LIMITE_ERRO)) from None

        documento = adaptador_oci.coletar(
            config,
            com_custo=regras.com_custo(job.tipo_job),
            dias_custo=regras.dias_de_custo(job.escopo),
            nome_cliente=nome_cliente,
        )
        config.clear()

        conteudo, sha256 = regras.compactar_documento(documento)
        resumo = regras.resumo_coleta(documento, time.monotonic() - inicio)
        with conn.transaction():
            conn.execute(sql.COLETA_BRUTA, {"job": job.id, "tamanho": len(conteudo), "sha256": sha256, "conteudo": conteudo})
            self.finalizar(conn, job.id, "COLETADO", erro=None, resumo=resumo, coletado=True)
        log.info("Job %s coletado: %s recurso(s), %s erro(s) não fatal(is), %s bytes.",
                 job.id, resumo["recursos"], resumo["erros_nao_fatais"], len(conteudo))

    def nome_cliente(self, conn, cliente_id: int) -> str | None:
        try:
            with conn.transaction():
                linha = conn.execute(sql.NOME_CLIENTE, {"cliente": cliente_id}).fetchone()
            return linha[0] if linha else None
        except Exception:  # noqa: BLE001 - sem permissão em cliente: segue sem o nome
            return None

    def finalizar(self, conn, job_id: int, status: str, *, erro: str | None, resumo: dict, coletado: bool = False) -> None:
        conn.execute(sql.FINALIZAR, {
            "status": status,
            "coletado": coletado,
            "erro": sanitizar(erro, LIMITE_ERRO) if erro else None,
            "resumo": json.dumps(resumo, ensure_ascii=False, default=str),
            "versao": self.versao[:30],
            "id": job_id,
        })


class Batimento(threading.Thread):
    """Heartbeat do job em conexão própria (o Zelador do worker usa heartbeat_em)."""

    def __init__(self, servico: Servico, job_id: int, intervalo: int):
        super().__init__(name=f"heartbeat-{job_id}", daemon=True)
        self.servico, self.job_id, self.intervalo = servico, job_id, intervalo
        self._fim = threading.Event()

    def run(self) -> None:
        conn = None
        while not self._fim.wait(self.intervalo):
            try:
                if conn is None:
                    conn = self.servico.conectar()
                conn.execute(sql.HEARTBEAT, {"id": self.job_id})
            except Exception as exc:  # noqa: BLE001
                log.warning("Heartbeat do job %s falhou: %s", self.job_id, type(exc).__name__)
                conn = None
        if conn is not None:
            try:
                conn.close()
            except Exception:  # noqa: BLE001
                pass

    def parar(self) -> None:
        self._fim.set()


def principal() -> int:
    configurar_logging()
    url = os.environ.get("CV_DB_URL", "").strip()
    if not url:
        log.critical("CV_DB_URL ausente. O coletor não tem o que fazer.")
        return 1

    chaves = ChavesMestras.de_ambiente(dict(os.environ))
    for problema in chaves.problemas:
        log.warning(problema)
    if not chaves.configurado:
        log.critical("Chave mestra (CV_MASTER_KEY_V{n}/CV_MASTER_KEY_ATUAL) ausente ou inválida.")
        return 1

    servico = Servico(url, chaves)
    try:
        servico.conectar().close()
    except Exception as exc:  # noqa: BLE001 - a mensagem pode ecoar a URL: só o tipo
        log.critical("Não foi possível conectar ao banco (CV_DB_URL). Tipo do erro: %s.", type(exc).__name__)
        return 1
    return servico.executar()
