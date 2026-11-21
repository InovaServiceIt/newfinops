import React, { useEffect, useState } from 'react';
import { Check, Copy, X } from 'lucide-react';
import { mensagemDeErro } from '../../services/api';
import { buscarRecurso, listarTags, rotuloTipo, type RecursoDetalhe } from '../../services/inventario';

const dataHora = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
const fmt = (iso: string | null) => (iso ? dataHora.format(new Date(iso)) : '—');

const ROTULO_ATRIBUTO: Record<string, string> = {
  shape: 'Shape', ocpus: 'OCPUs', memory_in_gbs: 'Memória (GB)', availability_domain: 'Availability domain',
  fault_domain: 'Fault domain', launch_mode: 'Launch mode', size_in_gbs: 'Tamanho (GB)', vpus_per_gb: 'VPUs/GB',
  anexado: 'Anexado', unique_size_in_gbs: 'Tamanho único (GB)', type: 'Tipo', source_type: 'Origem',
  expiration_time: 'Expira em', storage_tier: 'Camada', public_access_type: 'Acesso público', versioning: 'Versionamento',
  approximate_size: 'Tamanho aproximado (bytes)', approximate_count: 'Objetos (aprox.)', atribuido: 'Atribuído',
  lifetime: 'Tipo de IP', ip_address: 'Endereço IP', assigned_entity_type: 'Atribuído a', cidr_block: 'CIDR',
  not_after: 'Válido até', config_type: 'Configuração', protection_mode: 'Proteção', algorithm: 'Algoritmo',
  vault_type: 'Tipo de vault', kubernetes_version: 'Kubernetes', node_shape: 'Shape dos nós', node_count: 'Nós',
  shape_name: 'Shape', is_private: 'Privado',
};

function valor(v: string | number | boolean): string {
  if (typeof v === 'boolean') return v ? 'Sim' : 'Não';
  if (typeof v === 'number') return v.toLocaleString('pt-BR');
  return v;
}

/** Painel lateral com o detalhe do recurso (atributos, tags, OCID e proveniência). */
const PainelRecurso: React.FC<{ contaId: number; chave: string; onFechar: () => void }> = ({ contaId, chave, onFechar }) => {
  const [r, setR] = useState<RecursoDetalhe | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [copiado, setCopiado] = useState(false);

  useEffect(() => {
    let vivo = true;
    setR(null);
    setErro(null);
    buscarRecurso(contaId, chave)
      .then((d) => vivo && setR(d))
      .catch((e) => vivo && setErro(mensagemDeErro(e, 'Não foi possível carregar o recurso.')));
    return () => { vivo = false; };
  }, [contaId, chave]);

  useEffect(() => {
    const esc = (e: KeyboardEvent) => e.key === 'Escape' && onFechar();
    window.addEventListener('keydown', esc);
    return () => window.removeEventListener('keydown', esc);
  }, [onFechar]);

  const copiar = () => {
    navigator.clipboard?.writeText(chave).then(() => {
      setCopiado(true);
      window.setTimeout(() => setCopiado(false), 1500);
    }).catch(() => undefined);
  };

  const tags = listarTags(r?.tags);
  const atributos = Object.entries(r?.atributos ?? {});

  return (
    <div className="inv-painel-fundo" onClick={onFechar}>
      <aside className="inv-painel" role="dialog" aria-modal="true" aria-label="Detalhe do recurso" onClick={(e) => e.stopPropagation()}>
        <header className="inv-painel-topo">
          <div>
            <h2 className="inv-painel-titulo">{r?.nome ?? 'Recurso'}</h2>
            {r && <span className="inv-painel-sub" title={r.tipo}>{rotuloTipo(r.tipo)} · {r.regiao}</span>}
          </div>
          <button type="button" className="ui-btn ui-btn-ghost" onClick={onFechar} aria-label="Fechar"><X size={18} /></button>
        </header>

        {erro && <div className="ui-alert ui-alert-danger" role="alert">{erro}</div>}
        {!r && !erro && <div className="ui-skeleton inv-esqueleto" />}

        {r && (
          <div className="inv-painel-corpo">
            {r.removidoEm && <div className="ui-alert ui-alert-warning">Não encontrado na coleta de {fmt(r.removidoEm)} (removido).</div>}

            <div className="inv-painel-bloco">
              <h3>Identificação</h3>
              <div className="inv-ocid">
                <code>{r.chave}</code>
                <button type="button" className="ui-btn ui-btn-ghost" onClick={copiar} aria-label="Copiar OCID">
                  {copiado ? <Check size={14} /> : <Copy size={14} />} {copiado ? 'Copiado' : 'Copiar'}
                </button>
              </div>
              <dl className="inv-dl">
                <dt>Conta</dt><dd>{r.contaNome ?? '—'}</dd>
                <dt>Compartimento</dt><dd title={r.compartimentoOcid ?? ''}>{r.compartimento ?? r.compartimentoOcid ?? '—'}</dd>
                <dt>Estado</dt><dd>{r.estado ?? '—'}</dd>
                <dt>Criado na cloud</dt><dd>{fmt(r.criadoEmCloud)}</dd>
              </dl>
            </div>

            <div className="inv-painel-bloco">
              <h3>Atributos</h3>
              {atributos.length === 0 ? <p className="inv-nota">Sem atributos para este tipo.</p> : (
                <dl className="inv-dl">
                  {atributos.map(([k, v]) => (
                    <React.Fragment key={k}><dt title={k}>{ROTULO_ATRIBUTO[k] ?? k}</dt><dd>{valor(v)}</dd></React.Fragment>
                  ))}
                </dl>
              )}
            </div>

            <div className="inv-painel-bloco">
              <h3>Tags ({tags.length})</h3>
              {tags.length === 0 ? <p className="inv-nota">Sem tags.</p> : (
                <span className="inv-chips inv-chips-quebra">{tags.map((t) => <span key={t} className="inv-chip">{t}</span>)}</span>
              )}
            </div>

            <div className="inv-painel-bloco">
              <h3>Proveniência</h3>
              <dl className="inv-dl">
                <dt>Primeiro visto</dt><dd>{fmt(r.primeiroVistoEm)}</dd>
                <dt>Último visto</dt><dd>{fmt(r.ultimoVistoEm)}</dd>
                <dt>Job</dt><dd>{r.job !== null ? `#${r.job}` : '—'}</dd>
              </dl>
            </div>
          </div>
        )}
      </aside>
    </div>
  );
};

export default PainelRecurso;
