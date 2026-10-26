import { useState } from 'react';
import { Link, Navigate, Outlet, useLocation, useNavigate } from 'react-router';
import { AnimatePresence, motion } from 'motion/react';
import { useQueryClient } from '@tanstack/react-query';
import { LogOut } from 'lucide-react';
import { auth } from '@/api/endpoints';
import { getSession, setSession, useSession } from '@/api/session';
import { ConfirmDialog } from '@/components/ui';
import { contacts } from '@/config';
import { Logo } from '@/site/Logo';

export default function CabinetLayout() {
  const session = useSession('customer');
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [confirm, setConfirm] = useState(false);

  if (!session) return <Navigate to="/cabinet/login" replace state={{ from: location.pathname }} />;

  const logout = async () => {
    const refresh = getSession('customer')?.refreshToken;
    setSession('customer', null);
    queryClient.removeQueries({ queryKey: ['cabinet'] });
    navigate('/cabinet/login', { replace: true });
    if (refresh) auth.logout(refresh).catch(() => {});
  };

  const initials = session.user.fullName
    .split(' ')
    .map((p) => p[0])
    .slice(0, 2)
    .join('');

  return (
    <div className="min-h-screen bg-paper">
      <header className="sticky top-0 z-40 border-b border-line/70 bg-paper/85 backdrop-blur-xl">
        <div className="container-x flex h-16 items-center justify-between">
          <Link to="/cabinet" className="flex items-center gap-3">
            <Logo compact />
            <span className="hidden text-sm font-semibold sm:block">Личный кабинет</span>
          </Link>
          <div className="flex items-center gap-3">
            <Link to="/" className="hidden text-sm text-muted hover:text-ink sm:block">На сайт</Link>
            <div className="flex items-center gap-2.5 rounded-full border border-line bg-white py-1 pl-1 pr-3">
              <span className="flex size-8 items-center justify-center rounded-full bg-ink text-xs font-semibold text-gold">{initials}</span>
              <span className="max-w-[160px] truncate text-sm font-medium">{session.user.fullName}</span>
            </div>
            <button onClick={() => setConfirm(true)} className="flex size-9 items-center justify-center rounded-full text-muted transition hover:bg-ink/5 hover:text-ink" title="Выйти" aria-label="Выйти">
              <LogOut className="size-4" />
            </button>
          </div>
        </div>
      </header>

      <AnimatePresence mode="wait">
        <motion.main key={location.pathname} initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }} transition={{ duration: 0.25 }} className="container-x py-8 sm:py-10">
          <Outlet />
        </motion.main>
      </AnimatePresence>

      <footer className="container-x pb-10 text-center text-xs text-muted">
        Вопросы по оплате и документам — в чате сделки или по телефону <a className="tabular hover:text-ink" href={`tel:${contacts.phone.replace(/\s/g, '')}`}>{contacts.phone}</a>
      </footer>

      <ConfirmDialog open={confirm} onClose={() => setConfirm(false)} onConfirm={logout} title="Выйти из кабинета?" text="Чтобы войти снова, понадобится код из SMS." confirmLabel="Выйти" />
    </div>
  );
}
