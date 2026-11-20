import React, { useCallback, useEffect, useState } from 'react';
import { Pencil } from 'lucide-react';
import Modal from '../../components/common/Modal';
import { mensagemDeErro } from '../../services/api';
import { atualizarParametro, listarParametros, type Parametro } from '../../services/coletas';
import '../Contas/Contas.css';

const dataHora = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' });

/** Validação no navegador espelhando o backend (que é a autoridade). */
function validar(tipo: Parametro['tipo'], valor: string): string | null {
  const v = valor.trim();
  switch (tipo) {
    case 'INTEIRO': return /^-?\d+$/.test(v) ? null : 'Informe um número inteiro.';
    case 'DECIMAL': return /^-?\d+(\.\d+)?$/.test(v) ? null : 'Informe um número decimal com ponto (ex.: 1.25).';
    case 'BOOLEANO': return /^(true|false)$/i.test(v) ? null : 'Use true ou false.';
    case 'JSON': try { JSON.parse(v); return null; } catch { return 'JSON inválido.'; }
    default: return null;
  }
}

/** Parâmetros de negócio/operação (só ADMINISTRADOR; edição de chaves existentes). */
const ParametrosAba: React.FC = () => {
  const [itens, setItens] = useState<Parametro[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [editando, setEditando] = useState<Parametro | null>(null);
  const [valor, setValor] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [erroForm, setErroForm] = useState<string | null>(null);

  const carregar = useCallback(() => {
    setErro(null);
    listarParametros().then(setItens).catch((e) => setErro(mensagemDeErro(e, 'Não foi possível carregar os parâmetros.')));
  }, []);

  useEffect(carregar, [carregar]);

  const erroValor = editando ? validar(editando.tipo, valor) : null;

  const salvar = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editando || erroValor) return;
    setSalvando(true);
    setErroForm(null);
    try {
      await atualizarParametro(editando.chave, valor);
      setEditando(null);
      carregar();
    } catch (err) {
      setErroForm(mensagemDeErro(err, 'Não foi possível salvar o parâmetro.'));
    } finally {
      setSalvando(false);
    }
  };

  return (
    <section className="ui-card contas-painel">
      <p className="contas-ajuda">Regra de ouro: parâmetros de negócio e operação ficam aqui, no banco. No Coolify só segredos e o que é estático.</p>
      {erro && <div className="ui-alert ui-alert-danger" role="alert">{erro}</div>}
      {!itens && !erro && <div className="ui-skeleton contas-esqueleto" />}
      {itens && (
        <div className="ui-table-wrap">
          <table className="ui-table">
            <thead><tr><th>Chave</th><th>Valor</th><th>Tipo</th><th>Descrição</th><th>Atualizado</th><th className="contas-col-acoes">Ações</th></tr></thead>
            <tbody>
              {itens.map((p) => (
                <tr key={p.chave}>
                  <td><code className="contas-id">{p.chave}</code></td>
                  <td><code className="contas-id" title={p.valor ?? ''}>{p.valor ?? '—'}</code></td>
                  <td>{p.tipo}</td>
                  <td className="contas-ajuda">{p.descricao ?? '—'}</td>
                  <td className="contas-ajuda">{p.atualizadoEm ? dataHora.format(new Date(p.atualizadoEm)) : '—'}{p.atualizadoPor ? ` · ${p.atualizadoPor}` : ''}</td>
                  <td className="contas-col-acoes">
                    <button type="button" className="ui-btn ui-btn-ghost contas-icone" title="Editar" aria-label={`Editar ${p.chave}`}
                      onClick={() => { setEditando(p); setValor(p.valor ?? ''); setErroForm(null); }}><Pencil size={16} /></button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Modal
        aberto={editando !== null}
        titulo={`Parâmetro · ${editando?.chave ?? ''}`}
        onFechar={() => setEditando(null)}
        rodape={
          <>
            <button type="button" className="ui-btn ui-btn-secondary" onClick={() => setEditando(null)} disabled={salvando}>Cancelar</button>
            <button type="submit" form="form-parametro" className="ui-btn ui-btn-primary" disabled={salvando || !!erroValor}>{salvando ? 'Salvando...' : 'Salvar'}</button>
          </>
        }
      >
        {editando && (
          <form id="form-parametro" className="contas-form" onSubmit={salvar}>
            {editando.descricao && <p className="contas-ajuda">{editando.descricao}</p>}
            <div className="ui-field">
              <label className="ui-label" htmlFor="par-valor">Valor ({editando.tipo})</label>
              {editando.tipo === 'JSON' || editando.tipo === 'TEXTO' ? (
                <textarea id="par-valor" className="ui-input cred-textarea" spellCheck={false} value={valor} onChange={(e) => setValor(e.target.value)} />
              ) : editando.tipo === 'BOOLEANO' ? (
                <select id="par-valor" className="ui-select" value={valor.toLowerCase()} onChange={(e) => setValor(e.target.value)}>
                  <option value="true">true</option><option value="false">false</option>
                </select>
              ) : (
                <input id="par-valor" className="ui-input" inputMode="decimal" value={valor} onChange={(e) => setValor(e.target.value)} />
              )}
              {erroValor && <span className="contas-ajuda contas-ajuda-erro">{erroValor}</span>}
            </div>
            {erroForm && <div className="ui-alert ui-alert-danger" role="alert">{erroForm}</div>}
          </form>
        )}
      </Modal>
    </section>
  );
};

export default ParametrosAba;
