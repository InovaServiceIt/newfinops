"""Decifragem do cofre (contrato do MD 0.3, idêntico ao C# da API).

Blob v1: 0x01 | nonce(12) | ct | tag(16)  →  AESGCM(chave).decrypt(nonce, ct || tag, aad)
AAD da DEK:        cv:dek:v1:{cliente_id}:{versao_kek}
AAD da credencial: cv:cred:v1:{cloud_conta_id}:{tipo}
"""
from __future__ import annotations

import base64
import binascii
import re
from dataclasses import dataclass, field

VERSAO_FORMATO = 0x01
TAMANHO_CHAVE = 32
TAMANHO_NONCE = 12
TAMANHO_TAG = 16
PREFIXO_KEK = "CV_MASTER_KEY_V"
VARIAVEL_ATUAL = "CV_MASTER_KEY_ATUAL"


class CofreErro(Exception):
    """Falha de decifragem. Mensagem sempre genérica (nunca contém dados)."""


def aad_dek(cliente_id: int, versao_kek: int) -> bytes:
    return f"cv:dek:v1:{cliente_id}:{versao_kek}".encode("utf-8")


def aad_credencial(cloud_conta_id: int, tipo: str) -> bytes:
    return f"cv:cred:v1:{cloud_conta_id}:{tipo}".encode("utf-8")


def decifrar_v1(chave: bytes | bytearray, blob: bytes, aad: bytes) -> bytearray:
    """Decifra um blob v1 e devolve o texto claro em bytearray (para poder ser zerado)."""
    from cryptography.exceptions import InvalidTag
    from cryptography.hazmat.primitives.ciphers.aead import AESGCM

    if len(chave) != TAMANHO_CHAVE:
        raise CofreErro("Chave com tamanho inválido.")
    blob = bytes(blob)
    if len(blob) < 1 + TAMANHO_NONCE + TAMANHO_TAG:
        raise CofreErro("Conteúdo cifrado inválido.")
    if blob[0] != VERSAO_FORMATO:
        raise CofreErro("Versão do formato cifrado não suportada.")
    try:
        claro = AESGCM(bytes(chave)).decrypt(blob[1:1 + TAMANHO_NONCE], blob[1 + TAMANHO_NONCE:], aad)
    except InvalidTag:
        raise CofreErro("Falha na autenticação do conteúdo cifrado.") from None
    return bytearray(claro)


def zerar(buffer: bytearray | None) -> None:
    """Sobrescreve o buffer com zeros (melhor esforço: bytes/str do Python são imutáveis)."""
    if buffer is not None:
        for i in range(len(buffer)):
            buffer[i] = 0


@dataclass
class ChavesMestras:
    """KEKs de CV_MASTER_KEY_V{n} (base64 de 32 bytes) e CV_MASTER_KEY_ATUAL. Nunca loga valores."""

    chaves: dict[int, bytes] = field(default_factory=dict)
    atual: int | None = None
    problemas: list[str] = field(default_factory=list)

    @property
    def configurado(self) -> bool:
        return self.atual is not None and self.atual in self.chaves

    def kek(self, versao: int) -> bytes:
        try:
            return self.chaves[versao]
        except KeyError:
            raise CofreErro(f"KEK v{versao} não carregada.") from None

    @classmethod
    def de_ambiente(cls, ambiente: dict[str, str]) -> "ChavesMestras":
        r = cls()
        for nome, valor in ambiente.items():
            if not nome.upper().startswith(PREFIXO_KEK):
                continue
            sufixo = nome[len(PREFIXO_KEK):]
            if not sufixo.isdigit() or int(sufixo) <= 0:
                continue
            chave = _decodificar(valor)
            if chave is None:
                r.problemas.append(f"{nome} inválida: precisa ser base64 de exatamente 32 bytes (ignorada).")
                continue
            r.chaves[int(sufixo)] = chave

        atual = (ambiente.get(VARIAVEL_ATUAL) or "").strip()
        if not re.fullmatch(r"[0-9]+", atual):
            r.problemas.append(f"{VARIAVEL_ATUAL} ausente ou inválida.")
        elif int(atual) not in r.chaves:
            r.problemas.append(f"{VARIAVEL_ATUAL} aponta para a versão {int(atual)}, que não foi carregada.")
        else:
            r.atual = int(atual)
        return r


def _decodificar(valor: str | None) -> bytes | None:
    if not valor or not valor.strip():
        return None
    try:
        dados = base64.b64decode(valor.strip(), validate=True)
    except (binascii.Error, ValueError):
        return None
    return dados if len(dados) == TAMANHO_CHAVE else None
