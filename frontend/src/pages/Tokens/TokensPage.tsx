import React, { useEffect, useState } from 'react';
import { CheckCircle2, Info, AlertTriangle, XCircle } from 'lucide-react';
import PageHeader from '../../components/layout/PageHeader';
import { useTheme } from '../../theme/ThemeProvider';
import { THEMES } from '../../theme/themes';
import './TokensPage.css';

/** Tokens de cor exibidos como swatches (os valores vêm do skin ativo). */
const TOKEN_GROUPS: { title: string; tokens: string[] }[] = [
  { title: 'Fundo / superfície', tokens: ['--bg', '--surface', '--surface-2', '--surface-hover', '--overlay'] },
  { title: 'Texto', tokens: ['--text', '--text-muted', '--text-subtle', '--text-on-accent'] },
  { title: 'Borda', tokens: ['--border', '--border-strong', '--focus-ring'] },
  { title: 'Destaque', tokens: ['--accent', '--accent-hover', '--accent-soft', '--accent-2'] },
  {
    title: 'Status',
    tokens: ['--success', '--success-soft', '--warning', '--warning-soft', '--danger', '--danger-soft', '--info', '--info-soft'],
  },
  {
    title: 'Header',
    tokens: ['--header-bg', '--header-text', '--header-text-muted', '--header-border', '--header-control-bg', '--header-control-hover', '--header-brand-icon-bg'],
  },
  {
    title: 'Sidebar',
    tokens: ['--sidebar-bg', '--sidebar-border', '--sidebar-item', '--sidebar-item-hover-bg', '--sidebar-item-active-bg', '--sidebar-item-active-text', '--sidebar-section'],
  },
  { title: 'Gráficos', tokens: ['--chart-aws', '--chart-azure', '--chart-gcp', '--chart-oci', '--chart-forecast', '--chart-grid'] },
];

const EFFECT_TOKENS = ['--shadow-sm', '--shadow-md', '--radius-sm', '--radius-md', '--radius-lg', '--glass-blur'];

function readTokens(names: string[]): Record<string, string> {
  const style = getComputedStyle(document.documentElement);
  return Object.fromEntries(names.map((n) => [n, style.getPropertyValue(n).trim()]));
}

const ALL_TOKENS = [...TOKEN_GROUPS.flatMap((g) => g.tokens), ...EFFECT_TOKENS];

const ROWS = [
  { account: 'aws-prod-001', provider: 'AWS', cost: 'R$ 48.210,33', status: 'success', label: 'No orçamento' },
  { account: 'azure-sub-erp', provider: 'Azure', cost: 'R$ 31.902,10', status: 'warning', label: '92% do orçamento' },
  { account: 'gcp-analytics', provider: 'GCP', cost: 'R$ 12.477,85', status: 'danger', label: 'Estourou' },
] as const;

const TokensPage: React.FC = () => {
  const { theme, setTheme } = useTheme();
  const [values, setValues] = useState<Record<string, string>>({});

  // Relê os valores sempre que o skin muda.
  useEffect(() => {
    setValues(readTokens(ALL_TOKENS));
  }, [theme]);

  return (
    <div className="page-content">
      <PageHeader title="Tokens" subtitle="Amostras do skin ativo para conferir o equilíbrio das cores." />

      <div className="tokens-skin-switch">
        {THEMES.map((t) => (
          <button
            key={t.id}
            type="button"
            className={`ui-btn ${t.id === theme ? 'ui-btn-primary' : 'ui-btn-secondary'}`}
            onClick={() => setTheme(t.id)}
          >
            {t.name}
          </button>
        ))}
      </div>

      {/* ── Componentes ─────────────────────────────────────────────────── */}
      <section className="tokens-samples">
        <div className="ui-card">
          <h2 className="ui-card-title">Formulário</h2>
          <form className="tokens-form" onSubmit={(e) => e.preventDefault()}>
            <div className="ui-field">
              <label className="ui-label" htmlFor="tk-name">Centro de custo</label>
              <input id="tk-name" className="ui-input" placeholder="Ex.: Infraestrutura" />
            </div>
            <div className="ui-field">
              <label className="ui-label" htmlFor="tk-provider">Provedor</label>
              <select id="tk-provider" className="ui-select" defaultValue="aws">
                <option value="aws">AWS</option>
                <option value="azure">Azure</option>
                <option value="gcp">GCP</option>
                <option value="oci">OCI</option>
              </select>
            </div>
            <div className="tokens-form-actions">
              <button type="submit" className="ui-btn ui-btn-primary">Salvar</button>
              <button type="button" className="ui-btn ui-btn-secondary">Cancelar</button>
              <button type="button" className="ui-btn ui-btn-ghost">Limpar</button>
            </div>
          </form>
        </div>

        <div className="ui-card">
          <h2 className="ui-card-title">Badges e alertas</h2>
          <div className="tokens-badges">
            <span className="ui-badge ui-badge-success">Sucesso</span>
            <span className="ui-badge ui-badge-warning">Atenção</span>
            <span className="ui-badge ui-badge-danger">Crítico</span>
            <span className="ui-badge ui-badge-info">Informação</span>
            <span className="ui-badge ui-badge-accent">Destaque</span>
          </div>
          <div className="tokens-alerts">
            <div className="ui-alert ui-alert-info"><Info size={16} /> Dados de custo atualizados há 2 horas.</div>
            <div className="ui-alert ui-alert-success"><CheckCircle2 size={16} /> Orçamento dentro do previsto.</div>
            <div className="ui-alert ui-alert-warning"><AlertTriangle size={16} /> Consumo acima de 90% do orçamento.</div>
            <div className="ui-alert ui-alert-danger"><XCircle size={16} /> Anomalia de custo detectada.</div>
          </div>
        </div>

        <div className="ui-card tokens-table-card">
          <h2 className="ui-card-title">Tabela</h2>
          <table className="ui-table">
            <thead>
              <tr>
                <th>Conta</th>
                <th>Provedor</th>
                <th className="num">Custo (mês)</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {ROWS.map((r) => (
                <tr key={r.account}>
                  <td>{r.account}</td>
                  <td>{r.provider}</td>
                  <td className="num">{r.cost}</td>
                  <td><span className={`ui-badge ui-badge-${r.status}`}>{r.label}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      {/* ── Swatches ────────────────────────────────────────────────────── */}
      {TOKEN_GROUPS.map((group) => (
        <section key={group.title} className="tokens-group">
          <h2 className="tokens-group-title">{group.title}</h2>
          <div className="tokens-grid">
            {group.tokens.map((name) => (
              <div key={name} className="token-swatch">
                <div className="token-swatch-color" style={{ background: `var(${name})` }} />
                <div className="token-swatch-info">
                  <code className="token-name">{name}</code>
                  <code className="token-value">{values[name] || '—'}</code>
                </div>
              </div>
            ))}
          </div>
        </section>
      ))}

      <section className="tokens-group">
        <h2 className="tokens-group-title">Efeitos</h2>
        <div className="tokens-grid">
          {EFFECT_TOKENS.map((name) => (
            <div key={name} className="token-swatch">
              <div
                className="token-swatch-effect"
                style={
                  name.startsWith('--shadow')
                    ? { boxShadow: `var(${name})` }
                    : name.startsWith('--radius')
                      ? { borderRadius: `var(${name})` }
                      : undefined
                }
              />
              <div className="token-swatch-info">
                <code className="token-name">{name}</code>
                <code className="token-value">{values[name] || '—'}</code>
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
};

export default TokensPage;
