"""Gera tests/vetores/cofre_v1.json com a implementação de referência (pacote cryptography).

Formato do blob v1 (contrato CloudVista API <-> coletor):
    [0] = 0x01 | [1..12] = nonce (12 bytes) | [13..n-17] = texto cifrado | [n-16..n-1] = tag (16 bytes)

Uso:  python gerar_cofre_v1.py            (regrava o JSON ao lado deste arquivo)
      python gerar_cofre_v1.py --verificar (confere o JSON existente: cifra e decifra cada caso)

As chaves e nonces daqui são FIXOS e PÚBLICOS: servem só para teste, nunca para produção.
"""
import json
import pathlib
import sys

from cryptography.hazmat.primitives.ciphers.aead import AESGCM

ARQUIVO = pathlib.Path(__file__).with_name("cofre_v1.json")

CASOS = [
    {
        "nome": "credencial_aws_access_key",
        "chave_hex": "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f",
        "nonce_hex": "0f0e0d0c0b0a090807060504",
        "aad": "cv:cred:v1:42:AWS_ACCESS_KEY",
        "texto_claro": '{"access_key_id":"EXEMPLO-FALSO-NAO-E-CHAVE","secret_access_key":"SEGREDO_TESTE_valor_ficticio"}',
    },
    {
        "nome": "dek_cifrada_com_kek",
        "chave_hex": "ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100",
        "nonce_hex": "a1a2a3a4a5a6a7a8a9aaabac",
        # DEK de teste (32 bytes) em hex no texto claro (o vetor cifra os bytes, ver "texto_claro_hex").
        "aad": "cv:dek:v1:7:1",
        "texto_claro_hex": "5a" * 32,
    },
    {
        "nome": "texto_vazio_aad_utf8",
        "chave_hex": "4242424242424242424242424242424242424242424242424242424242424242",
        "nonce_hex": "000000000000000000000001",
        "aad": "cv:cred:v1:1:OCI_API_KEY ção",
        "texto_claro": "",
    },
]


def texto_claro_bytes(caso: dict) -> bytes:
    if "texto_claro_hex" in caso:
        return bytes.fromhex(caso["texto_claro_hex"])
    return caso["texto_claro"].encode("utf-8")


def cifrar_v1(chave: bytes, nonce: bytes, texto: bytes, aad: bytes) -> bytes:
    return b"\x01" + nonce + AESGCM(chave).encrypt(nonce, texto, aad)  # encrypt devolve ct || tag


def decifrar_v1(chave: bytes, blob: bytes, aad: bytes) -> bytes:
    assert blob[0] == 0x01, "versão do formato"
    return AESGCM(chave).decrypt(blob[1:13], blob[13:], aad)


def gerar() -> list:
    saida = []
    for caso in CASOS:
        chave = bytes.fromhex(caso["chave_hex"])
        nonce = bytes.fromhex(caso["nonce_hex"])
        aad = caso["aad"].encode("utf-8")
        blob = cifrar_v1(chave, nonce, texto_claro_bytes(caso), aad)
        saida.append({**caso, "blob_hex": blob.hex()})
    return saida


if __name__ == "__main__":
    if "--verificar" in sys.argv:
        for caso in json.loads(ARQUIVO.read_text(encoding="utf-8"))["casos"]:
            chave, aad = bytes.fromhex(caso["chave_hex"]), caso["aad"].encode("utf-8")
            blob = bytes.fromhex(caso["blob_hex"])
            assert decifrar_v1(chave, blob, aad) == texto_claro_bytes(caso), caso["nome"]
            assert cifrar_v1(chave, bytes.fromhex(caso["nonce_hex"]), texto_claro_bytes(caso), aad) == blob, caso["nome"]
        print("ok: todos os vetores conferem")
    else:
        conteudo = {
            "descricao": "Vetores AES-256-GCM do cofre CloudVista, formato v1: 0x01 | nonce(12) | ct | tag(16). "
                         "Gerados com o pacote Python 'cryptography' (AESGCM). Chaves e nonces FIXOS, só para teste.",
            "versao_formato": 1,
            "casos": gerar(),
        }
        ARQUIVO.write_text(json.dumps(conteudo, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"gravado: {ARQUIVO}")
