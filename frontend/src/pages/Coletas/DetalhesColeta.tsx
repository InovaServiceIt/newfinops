import React from 'react';
import type { Coleta } from '../../services/coletas';

// Bloco "Ver detalhes" da tela Coletas: erro, diagnóstico de custo e amostra de avisos do coletor.

interface QueryFalha { name: string; ok: boolean; truncated: boolean; erro?: string }
interface DiagnosticoCusto { status: 'ok' | 'parcial' | 'ausente'; motivo: string; blocos: Record<string, number>; queries_falhas: QueryFalha[] }
interface Aviso { area: string; operacao: string; status_http: number | null; mensagem: string }
interface Importacao { avisos?: string[] }

const STATUS_CUSTO: Record<DiagnosticoCusto['status'], { rotulo: string; classe: string }> = {
  ok: { rotulo: 'Custo OK', classe: 'ui-badge-success' },
  parcial: { rotulo: 'Custo parcial', classe: 'ui-badge-warning' },
  ausente: { rotulo: 'Sem custo', classe: 'ui-badge-warning' },
};

const numero = new Intl.NumberFormat('pt-BR');

export function diagnosticoCusto(c: Coleta): DiagnosticoCusto | null {
  const d = c.resumo?.custo as DiagnosticoCusto | undefined;
  return d && typeof d === 'object' && d.status in STATUS_CUSTO ? d : null;
}

function avisos(c: Coleta): Aviso[] {
  const a = c.resumo?.avisos_amostra;
  return Array.isArray(a) ? (a as Aviso[]) : [];
}

function importacao(c: Coleta): Importacao | null {
  const i = c.resumo?.importacao;
  return i && typeof i === 'object' ? (i as Importacao) : null;
}

/** Há algo para mostrar no "Ver detalhes"? */
export function temDetalhes(c: Coleta): boolean {
  return !!c.erro || !!diagnosticoCusto(c) || avisos(c).length > 0 || (importacao(c)?.avisos?.length ?? 0) > 0;
}

/** Badge curto ao lado do resumo (só quando o custo não veio completo). */
export const BadgeCusto: React.FC<{ c: Coleta }> = ({ c }) => {
  const d = diagnosticoCusto(c);
  if (!d || d.status === 'ok') return null;
  const s = STATUS_CUSTO[d.status];
  return <span className={`ui-badge ${s.classe} coletas-badge-custo`} title={d.motivo}>{s.rotulo}</span>;
};

const DetalhesColeta: React.FC<{ c: Coleta }> = ({ c }) => {
  const d = diagnosticoCusto(c);
  const lista = avisos(c);
  const imp = importacao(c);
  const totalAvisos = typeof c.resumo?.erros_nao_fatais === 'number' ? c.resumo.erros_nao_fatais : lista.length;

  return (
    <div className="coletas-detalhes">
      {c.erro && <div className="ui-alert ui-alert-danger">{c.erro}</div>}

      {d && (
        <div className="coletas-detalhe-bloco">
          <h4 className="coletas-detalhe-titulo">
            Custo <span className={`ui-badge ${STATUS_CUSTO[d.status].classe}`}>{STATUS_CUSTO[d.status].rotulo}</span>
          </h4>
          {d.motivo && <p className="coletas-detalhe-texto">{d.motivo}</p>}
          {Object.keys(d.blocos ?? {}).length > 0 && (
            <p className="coletas-sub">
              {Object.entries(d.blocos).map(([b, n]) => `${b}: ${numero.format(n)}`).join(' · ')}
            </p>
          )}
          {d.queries_falhas?.length > 0 && (
            <div className="ui-table-wrap">
              <table className="ui-table">
                <thead><tr><th>Consulta</th><th>Situação</th><th>Erro</th></tr></thead>
                <tbody>
                  {d.queries_falhas.map((q) => (
                    <tr key={q.name}>
                      <td><code>{q.name}</code></td>
                      <td>{!q.ok ? 'Falhou' : 'Truncada'}</td>
                      <td>{q.erro ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {imp?.avisos && imp.avisos.length > 0 && (
        <div className="coletas-detalhe-bloco">
          <h4 className="coletas-detalhe-titulo">Importação</h4>
          <ul className="coletas-detalhe-lista">{imp.avisos.map((a) => <li key={a}>{a}</li>)}</ul>
        </div>
      )}

      {lista.length > 0 && (
        <div className="coletas-detalhe-bloco">
          <h4 className="coletas-detalhe-titulo">
            Avisos do coletor {totalAvisos > lista.length ? `(amostra de ${lista.length} de ${numero.format(totalAvisos)})` : `(${lista.length})`}
          </h4>
          <div className="ui-table-wrap">
            <table className="ui-table">
              <thead><tr><th>Área</th><th>Operação</th><th className="num">HTTP</th><th>Mensagem</th></tr></thead>
              <tbody>
                {lista.map((a, i) => (
                  <tr key={i}>
                    <td><code>{a.area || '—'}</code></td>
                    <td>{a.operacao || '—'}</td>
                    <td className="num">{a.status_http ?? '—'}</td>
                    <td>{a.mensagem}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {c.coletorVersao && <span className="coletas-sub">Coletor {c.coletorVersao}</span>}
    </div>
  );
};

export default DetalhesColeta;
