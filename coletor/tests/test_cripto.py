import base64

import pytest

from cloudvista_coletor.cripto import (
    ChavesMestras, CofreErro, aad_credencial, aad_dek, decifrar_v1, zerar,
)
from conftest import cifrar_v1


def _texto(caso) -> bytes:
    if "texto_claro_hex" in caso:
        return bytes.fromhex(caso["texto_claro_hex"])
    return caso["texto_claro"].encode("utf-8")


def test_vetores_do_cofre_sao_decifrados(vetores):
    """Interoperabilidade: o MESMO arquivo que o C# reproduz (tests/vetores/cofre_v1.json)."""
    assert len(vetores) == 3
    for caso in vetores:
        claro = decifrar_v1(bytes.fromhex(caso["chave_hex"]), bytes.fromhex(caso["blob_hex"]), caso["aad"].encode("utf-8"))
        assert bytes(claro) == _texto(caso), caso["nome"]


def test_vetores_recusam_aad_errado(vetores):
    for caso in vetores:
        with pytest.raises(CofreErro):
            decifrar_v1(bytes.fromhex(caso["chave_hex"]), bytes.fromhex(caso["blob_hex"]), b"cv:cred:v1:999:OUTRO")


def test_aads_iguais_ao_contrato_da_api():
    assert aad_dek(7, 1) == b"cv:dek:v1:7:1"
    assert aad_credencial(42, "OCI_API_KEY") == b"cv:cred:v1:42:OCI_API_KEY"


def test_ida_e_volta_com_envelope_dek_kek():
    kek, dek = bytes(range(32)), bytes([9] * 32)
    dek_cifrada = cifrar_v1(kek, dek, aad_dek(5, 1))
    cred = cifrar_v1(dek, b'{"x":"SEGREDO_TESTE_envelope"}', aad_credencial(11, "AWS_ACCESS_KEY"))

    dek2 = decifrar_v1(kek, dek_cifrada, aad_dek(5, 1))
    assert bytes(decifrar_v1(dek2, cred, aad_credencial(11, "AWS_ACCESS_KEY"))) == b'{"x":"SEGREDO_TESTE_envelope"}'


def test_bit_adulterado_dek_errada_versao_e_tamanho():
    chave = bytes([1] * 32)
    blob = bytearray(cifrar_v1(chave, b"abc", b"aad"))
    adulterado = bytearray(blob)
    adulterado[-1] ^= 1
    with pytest.raises(CofreErro):
        decifrar_v1(chave, bytes(adulterado), b"aad")
    with pytest.raises(CofreErro):
        decifrar_v1(bytes([2] * 32), bytes(blob), b"aad")
    blob[0] = 2
    with pytest.raises(CofreErro, match="Versão"):
        decifrar_v1(chave, bytes(blob), b"aad")
    with pytest.raises(CofreErro):
        decifrar_v1(bytes(16), bytes(40), b"aad")


def test_erro_de_decifragem_nao_contem_dados():
    chave = bytes([1] * 32)
    blob = cifrar_v1(chave, b"SEGREDO_TESTE_msg", b"aad")
    with pytest.raises(CofreErro) as e:
        decifrar_v1(bytes([3] * 32), blob, b"aad")
    assert "SEGREDO_TESTE" not in str(e.value)


def test_zerar_bytearray():
    b = bytearray(b"SEGREDO_TESTE")
    zerar(b)
    assert b == bytearray(len(b))


def test_chaves_mestras_carrega_e_nunca_expoe_valor():
    boa = base64.b64encode(bytes(range(32))).decode()
    ruim = "SEGREDO_TESTE_kek_invalida"
    c = ChavesMestras.de_ambiente({"CV_MASTER_KEY_V1": boa, "CV_MASTER_KEY_V2": ruim, "CV_MASTER_KEY_ATUAL": "1", "PATH": "x"})
    assert c.configurado and c.atual == 1 and c.kek(1) == bytes(range(32))
    assert any("CV_MASTER_KEY_V2" in p for p in c.problemas)
    assert not any(ruim in p or boa in p for p in c.problemas)


@pytest.mark.parametrize("ambiente", [
    {},
    {"CV_MASTER_KEY_V1": base64.b64encode(bytes(32)).decode()},
    {"CV_MASTER_KEY_V1": base64.b64encode(bytes(32)).decode(), "CV_MASTER_KEY_ATUAL": "2"},
    {"CV_MASTER_KEY_V1": base64.b64encode(bytes(31)).decode(), "CV_MASTER_KEY_ATUAL": "1"},
])
def test_chaves_mestras_ausentes_ou_invalidas(ambiente):
    assert not ChavesMestras.de_ambiente(ambiente).configurado
