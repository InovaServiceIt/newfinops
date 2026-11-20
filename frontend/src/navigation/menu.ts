import {
  LayoutDashboard, Search, PieChart, Tags,
  Lightbulb, AlertTriangle, Wallet, Server,
  Receipt, Bot, Settings, Cloud,
  type LucideIcon,
} from 'lucide-react';

export interface MenuItem {
  title: string;
  path: string;
  icon: LucideIcon;
  /** Visível só para ADMINISTRADOR (o backend também garante). */
  adminOnly?: boolean;
}

export interface MenuSection {
  title: string;
  items: MenuItem[];
}

/** Menu da sidebar. Nesta fase todos os itens levam a uma página placeholder. */
export const MENU: MenuSection[] = [
  {
    title: 'Análise',
    items: [
      { title: 'Visão Geral', path: '/visao-geral', icon: LayoutDashboard },
      { title: 'Explorador de custos', path: '/explorador-de-custos', icon: Search },
      { title: 'Alocação & showback', path: '/alocacao-showback', icon: PieChart },
      { title: 'Governança de tags', path: '/governanca-de-tags', icon: Tags },
    ],
  },
  {
    title: 'Otimização',
    items: [
      { title: 'Recomendações', path: '/recomendacoes', icon: Lightbulb },
      { title: 'Anomalias', path: '/anomalias', icon: AlertTriangle },
      { title: 'Orçamentos', path: '/orcamentos', icon: Wallet },
      { title: 'Inventário multicloud', path: '/inventario-multicloud', icon: Server },
    ],
  },
  {
    title: 'Negócio',
    items: [
      { title: 'Faturamento (fator)', path: '/faturamento', icon: Receipt },
      { title: 'Copiloto IA', path: '/copiloto-ia', icon: Bot },
      { title: 'Contas de cloud', path: '/contas-de-cloud', icon: Cloud },
      { title: 'Configurações', path: '/configuracoes', icon: Settings, adminOnly: true },
    ],
  },
];

export const MENU_ITEMS: MenuItem[] = MENU.flatMap((s) => s.items);

export const HOME_PATH = MENU_ITEMS[0].path;
