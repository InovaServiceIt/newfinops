"""Integração com o coletor vendorizado (sem rede): o patch de autenticação atinge o run()."""
import pytest

pytest.importorskip("oci")

from cloudvista_coletor.adaptadores import oci as adaptador  # noqa: E402


def test_versao_do_coletor_do_mauricio():
    assert adaptador.versao_coletor_oci() == "1.48.0"


def test_patch_de_authenticate_atinge_o_modulo_usado_pelo_run_e_e_restaurado():
    auth_mod, collect_mod, _ = adaptador._importar_vendor()
    assert collect_mod.auth_mod is auth_mod  # o run() chama auth_mod.authenticate(...)
    original = auth_mod.authenticate
    contexto = object()
    with adaptador._authenticate_substituido(auth_mod, contexto):
        assert collect_mod.auth_mod.authenticate(prefer_config_profile="DEFAULT") is contexto
    assert auth_mod.authenticate is original


def test_coletar_usa_arquivo_temporario_apagado_e_contexto_em_memoria(monkeypatch, tmp_path):
    auth_mod, collect_mod, _ = adaptador._importar_vendor()
    capturado = {}

    def run_falso(out_path, profile, *args, **kwargs):
        capturado["out_path"] = out_path
        capturado["auth"] = collect_mod.auth_mod.authenticate(prefer_config_profile=profile)
        capturado["kwargs"] = kwargs
        open(out_path, "w").write("{}")
        print("saída de terminal do coletor (descartada)")
        return {"ok": True}

    monkeypatch.setattr(adaptador, "contexto_auth", lambda config: ("CTX", config["tenancy"]))
    monkeypatch.setattr(collect_mod, "run", run_falso)

    doc = adaptador.coletar({"tenancy": "t"}, com_custo=True, dias_custo=30, nome_cliente="Ultragaz")
    import os
    assert doc == {"ok": True}
    assert capturado["auth"] == ("CTX", "t")
    assert capturado["kwargs"]["with_cost"] is True and capturado["kwargs"]["cost_days"] == 30
    assert capturado["kwargs"]["executed_by"] == "CloudVista" and capturado["kwargs"]["client_name"] == "Ultragaz"
    assert not os.path.exists(capturado["out_path"])  # temporário apagado


def test_contexto_auth_valida_config_sem_rede(monkeypatch):
    import oci
    monkeypatch.setattr(adaptador, "regiao_home", lambda config: "sa-saopaulo-1")
    with pytest.raises(oci.exceptions.InvalidConfig):
        adaptador.contexto_auth({"user": "x", "tenancy": "y", "fingerprint": "z", "region": "r"})  # sem chave
