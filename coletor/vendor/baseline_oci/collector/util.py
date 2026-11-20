"""Utilitários compartilhados pelos coletores (somente leitura)."""

from __future__ import annotations

from typing import Any, Callable, List

import oci


def to_dict(obj: Any) -> Any:
    """Converte modelos do SDK OCI em estruturas serializáveis em JSON."""
    return oci.util.to_dict(obj)


def paginate(list_func: Callable, *args, **kwargs) -> List[Any]:
    """Executa uma operação List paginada e retorna a lista completa de dados.

    Usa o helper oficial do SDK, que segue o header opc-next-page. Apenas
    operações List/Get (read-only) devem ser passadas aqui.
    """
    return oci.pagination.list_call_get_all_results(list_func, *args, **kwargs).data


class ErrorSink:
    """Coleta erros não-fatais para gravar no JSON, sem abortar a coleta.

    Falha em um serviço/compartment/região não deve derrubar o baseline inteiro;
    o relatório precisa saber o que faltou.
    """

    def __init__(self) -> None:
        self.items: List[dict] = []

    def record(self, *, scope: str, region: str, compartment_id: str, error: Exception) -> None:
        code = getattr(error, "status", None) or getattr(error, "code", None)
        self.items.append(
            {
                "scope": scope,
                "region": region,
                "compartment_id": compartment_id,
                "error_type": type(error).__name__,
                "error_code": str(code) if code is not None else None,
                "message": str(error),
            }
        )

    def as_list(self) -> List[dict]:
        return list(self.items)
