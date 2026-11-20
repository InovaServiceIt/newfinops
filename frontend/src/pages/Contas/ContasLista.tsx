import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Cloud, Pencil } from 'lucide-react';
import { mensagemDeErro } from '../../services/api';
import { listarContas, type ContaCloud, type Provedor } from '../../services/contas';
import { LISTA_PROVEDORES, PROVEDORES, ROTULO_ESCOPO } from './provedores';
import './Contas.css';

interface ContasListaProps {
  /** Com ações de edição e ativação (aba de Configurações do administrador). */
  gerenciavel?: boolean;
  onEditar?: (c: ContaCloud) => void;
  onAlternarAtivo?: (c: ContaCloud) => void;
  /** Botões à direita da barra de filtros (ex.: "Nova conta", "Gerenciar"). */
  acoes?: React.ReactNode;
  /** Muda para forçar recarga (após salvar). */
  versao?: number;
}

/** Lista de contas de cloud com filtros por cliente e provedor. O RLS do backend define o que cada perfil vê. */
const ContasLista: React.FC<ContasListaProps> = ({ gerenciavel, onEditar, onAlternarAtivo, acoes, versao = 0 }) => {
  const [contas, setContas] = useState<ContaCloud[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [cliente, setCliente] = useState<string>('');
  const [provedor, setProvedor] = useState<Provedor | ''>('');
  const [incluirInativas, setIncluirInativas] = useState(false);

  const carregar = useCallback(() => {
    setErro(null);
    listarContas({ incluirInativas })
      .then(setContas)
      .catch((e) => setErro(mensagemDeErro(e, 'Não foi possível carregar as contas.')));
  }, [incluirInativas]);

  useEffect(carregar, [carregar, versao]);

  const clientes = useMemo(() => {
    const mapa = new Map<number, string>();
    (contas ?? []).forEach((c) => mapa.set(c.clienteId, c.clienteNome));
    return Array.from(mapa, ([id, nome]) => ({ id, nome })).sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'));
  }, [contas]);

  const visiveis = useMemo(
    () => (contas ?? []).filter((c) => (!cliente || String(c.clienteId) === cliente) && (!provedor || c.provedor === provedor)),
    [contas, cliente, provedor],
  );

  return (
    <section className="ui-card contas-painel">
      <div className="contas-barra">
        <div className="ui-field">
          <label className="ui-label" htmlFor="contas-cliente">Cliente</label>
          <select id="contas-cliente" className="ui-select" value={cliente} onChange={(e) => setCliente(e.target.value)}>
            <option value="">Todos</option>
            {clientes.map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
          </select>
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="contas-provedor">Provedor</label>
          <select id="contas-provedor" className="ui-select" value={provedor} onChange={(e) => setProvedor(e.target.value as Provedor | '')}>
            <option value="">Todos</option>
            {LISTA_PROVEDORES.map((p) => <option key={p} value={p}>{PROVEDORES[p].rotulo}</option>)}
          </select>
        </div>
        <label className="ui-switch contas-inativas">
          <input type="checkbox" checked={incluirInativas} onChange={(e) => setIncluirInativas(e.target.checked)} />
          <span className="ui-switch-track" />
          Mostrar inativas
        </label>
        {acoes && <div className="contas-acoes">{acoes}</div>}
      </div>

      {erro && <div className="ui-alert ui-alert-danger" role="alert">{erro}</div>}
      {!contas && !erro && <div className="ui-skeleton contas-esqueleto" />}
      {contas && visiveis.length === 0 && (
        <div className="ui-empty">{contas.length === 0 ? 'Nenhuma conta de cloud visível para você.' : 'Nenhuma conta para o filtro.'}</div>
      )}
      {contas && visiveis.length > 0 && (
        <div className="ui-table-wrap">
          <table className="ui-table">
            <thead>
              <tr>
                <th>Cliente</th><th>Provedor</th><th>Nome</th><th>Identificador</th><th>Escopo</th><th>Região</th><th>Situação</th>
                {gerenciavel && <th className="contas-col-acoes">Ações</th>}
              </tr>
            </thead>
            <tbody>
              {visiveis.map((c) => (
                <tr key={c.id}>
                  <td>{c.clienteNome}</td>
                  <td>
                    <span className="contas-provedor"><Cloud size={14} />{PROVEDORES[c.provedor]?.rotulo ?? c.provedor}</span>
                  </td>
                  <td>{c.nome}</td>
                  <td><code className="contas-id" title={c.identificador}>{c.identificador}</code></td>
                  <td>{ROTULO_ESCOPO[c.escopo] ?? c.escopo}</td>
                  <td>{c.regiaoPadrao ?? '—'}</td>
                  <td><span className={`ui-badge ${c.ativo ? 'ui-badge-success' : 'ui-badge-warning'}`}>{c.ativo ? 'Ativa' : 'Inativa'}</span></td>
                  {gerenciavel && (
                    <td className="contas-col-acoes">
                      <button type="button" className="ui-btn ui-btn-ghost" onClick={() => onEditar?.(c)} aria-label={`Editar ${c.nome}`}>
                        <Pencil size={14} /> Editar
                      </button>
                      <button type="button" className="ui-btn ui-btn-ghost" onClick={() => onAlternarAtivo?.(c)}>
                        {c.ativo ? 'Desativar' : 'Ativar'}
                      </button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
};

export default ContasLista;
