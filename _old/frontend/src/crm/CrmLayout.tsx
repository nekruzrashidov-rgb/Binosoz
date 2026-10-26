import { useEffect, useState } from 'react';
import { Link, Navigate, NavLink, Outlet, useLocation, useNavigate } from 'react-router';
import { AnimatePresence, motion } from 'motion/react';
import { useQueryClient } from '@tanstack/react-query';
import { ChevronsLeft, ExternalLink, LogOut, Menu, X } from 'lucide-react';
import { auth } from '@/api/endpoints';
import { getSession, setSession } from '@/api/session';
import { ConfirmDialog } from '@/components/ui';
import { cn } from '@/lib/cn';
import { roleLabel } from '@/lib/format';
import { Logo } from '@/site/Logo';
import { can, NAV, useStaff } from './access';

export default function CrmLayout() {
  const { session, roles } = useStaff();
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem('sitora.crm.collapsed') === '1';
    } catch {
      return false;
    }
  });
  const [mobile, setMobile] = useState(false);
  const [confirm, setConfirm] = useState(false);

  useEffect(() => {
    setMobile(false);
  }, [location.pathname]);
  useEffect(() => {
    try {
      localStorage.setItem('sitora.crm.collapsed', collapsed ? '1' : '0');
    } catch {
      /* не критично */
    }
  }, [collapsed]);

  if (!session) return <Navigate to="/crm/login" replace state={{ from: location.pathname }} />;

  const logout = () => {
    const refresh = getSession('staff')?.refreshToken;
    setSession('staff', null);
    queryClient.clear();
    navigate('/crm/login', { replace: true });
    if (refresh) auth.logout(refresh).catch(() => {});
  };

  const items = NAV.filter((n) => can(roles, n.roles));
  const groups = [...new Set(items.map((i) => i.group))];

  const sidebar = (compact: boolean) => (
    <div className="flex h-full flex-col">
      <div className={cn('flex h-16 items-center', compact ? 'justify-center' : 'px-5')}>
        <Link to="/crm">
          <Logo light compact={compact} />
        </Link>
      </div>
      <nav className="scrollbar-thin flex-1 overflow-y-auto px-3 py-2">
        {groups.map((g) => (
          <div key={g} className="mb-4">
            {!compact && <p className="mb-1.5 px-3 text-[10px] font-semibold uppercase tracking-[0.2em] text-white/30">{g}</p>}
            {items
              .filter((i) => i.group === g)
              .map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.to === '/crm'}
                  title={compact ? item.label : undefined}
                  className={({ isActive }) =>
                    cn(
                      'group relative mb-0.5 flex h-10 items-center gap-3 rounded-xl text-sm font-medium transition-colors',
                      compact ? 'justify-center' : 'px-3',
                      isActive ? 'text-white' : 'text-white/55 hover:bg-white/[0.05] hover:text-white',
                    )
                  }
                >
                  {({ isActive }) => (
                    <>
                      {isActive && <motion.span layoutId="crm-nav" className="absolute inset-0 rounded-xl bg-white/[0.09]" transition={{ type: 'spring', bounce: 0.15, duration: 0.45 }} />}
                      {isActive && <motion.span layoutId="crm-nav-bar" className="absolute -left-3 h-5 w-1 rounded-r-full bg-gold" />}
                      <item.icon className={cn('relative size-[18px] shrink-0 transition-transform group-hover:scale-110', isActive && 'text-gold')} />
                      {!compact && <span className="relative">{item.label}</span>}
                    </>
                  )}
                </NavLink>
              ))}
          </div>
        ))}
      </nav>
      <div className="border-t border-white/10 p-3">
        <div className={cn('flex items-center gap-3 rounded-xl p-2', compact && 'justify-center')}>
          <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-gold text-sm font-semibold text-ink">
            {session.user.fullName.split(' ').map((p) => p[0]).slice(0, 2).join('')}
          </span>
          {!compact && (
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium text-white">{session.user.fullName}</p>
              <p className="truncate text-xs text-white/45">{roles.map((r) => roleLabel[r]).join(', ')}</p>
            </div>
          )}
          {!compact && (
            <button onClick={() => setConfirm(true)} className="rounded-lg p-2 text-white/45 transition hover:bg-white/10 hover:text-white" title="Выйти" aria-label="Выйти">
              <LogOut className="size-4" />
            </button>
          )}
        </div>
      </div>
    </div>
  );

  return (
    <div className="min-h-screen bg-[#f4f2ee]">
      <motion.aside
        animate={{ width: collapsed ? 76 : 256 }}
        transition={{ type: 'spring', bounce: 0, duration: 0.35 }}
        className="fixed inset-y-0 left-0 z-40 hidden bg-ink lg:block"
      >
        {sidebar(collapsed)}
        <button
          onClick={() => setCollapsed((v) => !v)}
          className="absolute -right-3 top-20 flex size-6 items-center justify-center rounded-full border border-line bg-white text-ink shadow-soft transition hover:scale-110"
          aria-label={collapsed ? 'Развернуть меню' : 'Свернуть меню'}
        >
          <ChevronsLeft className={cn('size-3.5 transition-transform', collapsed && 'rotate-180')} />
        </button>
      </motion.aside>

      <AnimatePresence>
        {mobile && (
          <div className="fixed inset-0 z-50 lg:hidden">
            <motion.div className="absolute inset-0 bg-ink/50" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} onClick={() => setMobile(false)} />
            <motion.aside initial={{ x: -280 }} animate={{ x: 0 }} exit={{ x: -280 }} transition={{ type: 'spring', bounce: 0, duration: 0.35 }} className="absolute inset-y-0 left-0 w-[264px] bg-ink">
              {sidebar(false)}
              <button onClick={() => setMobile(false)} className="absolute right-3 top-4 rounded-lg p-2 text-white/60 hover:bg-white/10" aria-label="Закрыть меню">
                <X className="size-4" />
              </button>
            </motion.aside>
          </div>
        )}
      </AnimatePresence>

      <div
        style={{ '--sidebar': `${collapsed ? 76 : 256}px` } as React.CSSProperties}
        className="transition-[padding] duration-300 ease-out lg:pl-[var(--sidebar)]"
      >
        <header className="sticky top-0 z-30 flex h-14 items-center justify-between border-b border-line/70 bg-[#f4f2ee]/85 px-4 backdrop-blur-xl sm:px-8 lg:hidden">
          <button onClick={() => setMobile(true)} className="rounded-lg p-2 hover:bg-ink/5" aria-label="Меню">
            <Menu className="size-5" />
          </button>
          <Logo compact />
          <button onClick={() => setConfirm(true)} className="rounded-lg p-2 hover:bg-ink/5" aria-label="Выйти">
            <LogOut className="size-4" />
          </button>
        </header>
        <div className="flex justify-end px-4 pt-4 sm:px-8">
          <Link to="/" target="_blank" className="inline-flex items-center gap-1.5 text-xs text-muted hover:text-ink">
            Открыть сайт <ExternalLink className="size-3" />
          </Link>
        </div>
        <AnimatePresence mode="wait">
          <motion.main key={location.pathname} initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }} transition={{ duration: 0.2 }} className="px-4 pb-16 pt-2 sm:px-8">
            <Outlet />
          </motion.main>
        </AnimatePresence>
      </div>

      <ConfirmDialog open={confirm} onClose={() => setConfirm(false)} onConfirm={logout} title="Выйти из CRM?" confirmLabel="Выйти" />
    </div>
  );
}
