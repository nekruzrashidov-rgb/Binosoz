import type { ComponentType } from 'react';
import {
  Building,
  ClipboardList,
  Coins,
  FileSpreadsheet,
  Grid3x3,
  HardHat,
  Handshake,
  KanbanSquare,
  LayoutDashboard,
  ScrollText,
  UserCog,
  Users,
} from 'lucide-react';
import { useSession } from '@/api/session';
import type { Role } from '@/api/types';

// Права те же, что на сервере (Controllers/Roles.cs); Admin входит во все наборы.
export const ROLES = {
  sales: ['Manager', 'Admin'],
  finance: ['Accountant', 'Admin'],
  dealStaff: ['Manager', 'Accountant', 'Admin'],
  content: ['Content', 'Admin'],
  construction: ['Manager', 'Accountant', 'Content', 'Admin'],
  admin: ['Admin'],
} satisfies Record<string, Role[]>;

export interface NavItem {
  to: string;
  label: string;
  icon: ComponentType<{ className?: string }>;
  roles: Role[];
  group: string;
}

export const NAV: NavItem[] = [
  { to: '/crm', label: 'Обзор', icon: LayoutDashboard, roles: ROLES.sales, group: 'Продажи' },
  { to: '/crm/pipeline', label: 'Воронка', icon: KanbanSquare, roles: ROLES.sales, group: 'Продажи' },
  { to: '/crm/leads', label: 'Заявки', icon: ClipboardList, roles: ROLES.sales, group: 'Продажи' },
  { to: '/crm/apartments', label: 'Шахматка', icon: Grid3x3, roles: ROLES.sales, group: 'Продажи' },
  { to: '/crm/deals', label: 'Сделки', icon: Handshake, roles: ROLES.sales, group: 'Продажи' },
  { to: '/crm/customers', label: 'Клиенты', icon: Users, roles: ROLES.sales, group: 'Продажи' },
  { to: '/crm/reports', label: 'Отчёты', icon: FileSpreadsheet, roles: ROLES.finance, group: 'Финансы' },
  { to: '/crm/rates', label: 'Курс USD', icon: Coins, roles: ROLES.finance, group: 'Финансы' },
  { to: '/crm/catalog', label: 'Каталог', icon: Building, roles: ROLES.content, group: 'Объекты' },
  { to: '/crm/construction', label: 'Стройка', icon: HardHat, roles: ROLES.construction, group: 'Объекты' },
  { to: '/crm/audit', label: 'Журнал', icon: ScrollText, roles: ROLES.admin, group: 'Система' },
  { to: '/crm/users', label: 'Сотрудники', icon: UserCog, roles: ROLES.admin, group: 'Система' },
];

export const can = (roles: Role[] | undefined, allowed: Role[]) => !!roles?.some((r) => allowed.includes(r));

export function useStaff() {
  const session = useSession('staff');
  const roles = session?.user.roles ?? [];
  return { session, user: session?.user, roles, can: (allowed: Role[]) => can(roles, allowed) };
}

// Стартовая страница по роли: бухгалтер — в отчёты, контент — в каталог.
export function homeFor(roles: Role[]) {
  return NAV.find((n) => can(roles, n.roles))?.to ?? '/crm';
}

