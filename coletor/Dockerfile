# cloudvista-collector (Python 3.12). Sem porta HTTP.
# Build context: raiz do repositório. Dockerfile: coletor/Dockerfile
FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    TZ=America/Sao_Paulo

WORKDIR /app

COPY coletor/requirements.txt coletor/requirements.txt
RUN pip install --no-cache-dir -r coletor/requirements.txt

COPY coletor/cloudvista_coletor coletor/cloudvista_coletor
COPY coletor/vendor coletor/vendor

# Usuário sem privilégios; /tmp é o único lugar gravável usado (JSON temporário da coleta, apagado no fim).
RUN useradd --system --uid 10001 --home-dir /nonexistent --shell /usr/sbin/nologin coletor
USER coletor

WORKDIR /app/coletor
# CV_DB_URL, CV_MASTER_KEY_V{n} e CV_MASTER_KEY_ATUAL entram por variáveis do Coolify.
ENTRYPOINT ["python", "-m", "cloudvista_coletor"]
