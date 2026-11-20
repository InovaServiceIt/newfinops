import base64
import json
import os
import sys
from pathlib import Path

import pytest

RAIZ_COLETOR = Path(__file__).resolve().parents[1]
RAIZ_REPO = RAIZ_COLETOR.parent
sys.path.insert(0, str(RAIZ_COLETOR))

VETORES = RAIZ_REPO / "tests" / "vetores" / "cofre_v1.json"


@pytest.fixture
def vetores():
    return json.loads(VETORES.read_text(encoding="utf-8"))["casos"]


def cifrar_v1(chave: bytes, texto: bytes, aad: bytes) -> bytes:
    """Cifra no formato v1 com nonce aleatório (como a API faz)."""
    from cryptography.hazmat.primitives.ciphers.aead import AESGCM

    nonce = os.urandom(12)
    return b"\x01" + nonce + AESGCM(chave).encrypt(nonce, texto, aad)


def kek_b64(chave: bytes) -> str:
    return base64.b64encode(chave).decode()
