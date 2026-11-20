"""Coletor do Resource Scheduler (por região): agendas de start/stop.

Agendas nativas de ligar/desligar recursos em horários (`list_schedules`, GET
por compartment): ação, recorrência (cron/ICS), próxima/última execução com
status e os alvos (OCIDs diretos ou filtros). Custo zero: o serviço de
agendamento não tem SKU próprio (cobram os recursos, não a agenda) e a
listagem é control-plane. LIMITE HONESTO: automações fora do serviço nativo
(Functions com cron, scripts, ferramentas de terceiros) não são detectáveis
por API — o baseline mostra apenas o que está no Resource Scheduler.
"""

from __future__ import annotations

from typing import List

import oci

from ..auth import AuthContext
from ..util import ErrorSink, paginate, to_dict


def collect_scheduler(
    auth: AuthContext, region: str, compartment_ids: List[str], errors: ErrorSink
) -> dict:
    client = oci.resource_scheduler.ScheduleClient(**auth.client_kwargs(region))
    result: dict = {"schedules": []}

    for cid in compartment_ids:
        try:
            result["schedules"].extend(
                to_dict(s) for s in paginate(client.list_schedules, compartment_id=cid)
            )
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="scheduler.schedules", region=region, compartment_id=cid, error=exc)

    return result
