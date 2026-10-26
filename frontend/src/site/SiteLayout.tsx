import { useEffect, useState } from 'react';
import { Link, NavLink, Outlet, useLocation } from 'react-router';
import { AnimatePresence, motion, useScroll, useMotionValueEvent } from 'motion/react';
import { ArrowUpRight, Menu, Phone, UserRound, X } from 'lucide-react';
import { setLang, useLang } from '@/api/session';
import { Button } from '@/components/ui';
import { cn } from '@/lib/cn';
import { LeadModalProvider, useLeadModal } from './LeadModal';
import { useMainBuilding } from './hooks';
import { Logo } from './Logo';
import { contacts } from '@/config';

export const CONTACT_PHONE = contacts.phone;

export function SiteLayout() {
  return (
    <LeadModalProvider>
      <ScrollToTop />
      <Header />
      <main className="min-h-screen">
        <Outlet />
      </main>
      <Footer />
    </LeadModalProvider>
  );
}

function ScrollToTop() {
  const { pathname } = useLocation();
  useEffect(() => {
    window.scrollTo({ top: 0 });
  }, [pathname]);
  return null;
}

function Header() {
  const { pathname } = useLocation();
  const { data: building } = useMainBuilding();
  const openLead = useLeadModal();
  const { scrollY } = useScroll();
  const [scrolled, setScrolled] = useState(false);
  const [hidden, setHidden] = useState(false);
  const [menu, setMenu] = useState(false);
  // Над тёмным первым экраном главной шапка прозрачная и светлая.
  const overDark = (pathname === '/' || pathname.startsWith('/building')) && !scrolled;

  useMotionValueEvent(scrollY, 'change', (y) => {
    const prev = scrollY.getPrevious() ?? 0;
    setScrolled(y > 24);
    setHidden(y > 320 && y > prev && !menu);
  });

  useEffect(() => {
    setMenu(false);
  }, [pathname]);

  const links = [
    { to: building ? `/building/${building.id}` : '/apartments', label: 'Выбрать на 3D' },
    { to: '/apartments', label: 'Квартиры' },
    { to: '/contacts', label: 'Контакты' },
  ];

  return (
    <>
      <motion.header
        animate={{ y: hidden ? -90 : 0 }}
        transition={{ duration: 0.35, ease: [0.22, 1, 0.36, 1] }}
        className={cn(
          'fixed inset-x-0 top-0 z-50 transition-[background-color,box-shadow,color] duration-300',
          overDark ? 'text-white' : 'bg-paper/80 text-ink shadow-[0_1px_0_rgb(15_17_21/0.06)] backdrop-blur-xl',
        )}
      >
        <div className="container-x flex h-[72px] items-center justify-between gap-6">
          <Link to="/" className="shrink-0" aria-label="На главную">
            <Logo light={overDark} />
          </Link>

          <nav className="hidden items-center gap-1 lg:flex">
            {links.map((l) => (
              <NavLink
                key={l.label}
                to={l.to}
                className={({ isActive }) =>
                  cn(
                    'relative rounded-full px-4 py-2 text-sm font-medium transition-colors',
                    overDark ? 'text-white/75 hover:text-white' : 'text-ink/65 hover:text-ink',
                    isActive && (overDark ? 'text-white' : 'text-ink'),
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    {l.label}
                    {isActive && <motion.span layoutId="nav-dot" className="absolute -bottom-0.5 left-1/2 size-1 -translate-x-1/2 rounded-full bg-gold" />}
                  </>
                )}
              </NavLink>
            ))}
          </nav>

          <div className="flex items-center gap-2">
            <LangSwitch dark={overDark} />
            <a
              href={`tel:${CONTACT_PHONE.replace(/\s/g, '')}`}
              className={cn('hidden items-center gap-2 px-3 text-sm font-medium tabular xl:flex', overDark ? 'text-white/80 hover:text-white' : 'text-ink/70 hover:text-ink')}
            >
              <Phone className="size-4" /> {CONTACT_PHONE}
            </a>
            <Link
              to="/cabinet"
              className={cn(
                'hidden size-10 items-center justify-center rounded-full border transition-colors sm:flex',
                overDark ? 'border-white/25 hover:bg-white/10' : 'border-line bg-white hover:border-ink/25',
              )}
              title="Личный кабинет"
              aria-label="Личный кабинет"
            >
              <UserRound className="size-4" />
            </Link>
            <Button variant={overDark ? 'gold' : 'primary'} className="hidden sm:inline-flex" onClick={() => openLead()}>
              Оставить заявку
            </Button>
            <button
              onClick={() => setMenu((v) => !v)}
              className={cn('flex size-10 items-center justify-center rounded-full lg:hidden', overDark ? 'hover:bg-white/10' : 'hover:bg-ink/5')}
              aria-label="Меню"
            >
              <AnimatePresence mode="wait" initial={false}>
                <motion.span key={menu ? 'x' : 'm'} initial={{ rotate: -90, opacity: 0 }} animate={{ rotate: 0, opacity: 1 }} exit={{ rotate: 90, opacity: 0 }} transition={{ duration: 0.15 }}>
                  {menu ? <X className="size-5" /> : <Menu className="size-5" />}
                </motion.span>
              </AnimatePresence>
            </button>
          </div>
        </div>
      </motion.header>

      <AnimatePresence>
        {menu && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-40 flex flex-col bg-ink px-6 pb-10 pt-28 text-white lg:hidden"
          >
            <nav className="flex flex-col gap-2">
              {[...links, { to: '/cabinet', label: 'Личный кабинет' }].map((l, i) => (
                <motion.div key={l.label} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.05 + i * 0.05 }}>
                  <Link to={l.to} className="flex items-center justify-between border-b border-white/10 py-4 font-display text-3xl">
                    {l.label} <ArrowUpRight className="size-5 text-gold" />
                  </Link>
                </motion.div>
              ))}
            </nav>
            <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ delay: 0.3 }} className="mt-auto flex flex-col gap-3">
              <a href={`tel:${CONTACT_PHONE.replace(/\s/g, '')}`} className="text-lg tabular text-white/80">
                {CONTACT_PHONE}
              </a>
              <Button
                variant="gold"
                size="lg"
                onClick={() => {
                  setMenu(false);
                  openLead();
                }}
              >
                Оставить заявку
              </Button>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </>
  );
}

function LangSwitch({ dark }: { dark: boolean }) {
  const lang = useLang();
  return (
    <div className={cn('flex rounded-full border p-0.5 text-[11px] font-semibold', dark ? 'border-white/25' : 'border-line bg-white')}>
      {(['ru', 'tg'] as const).map((l) => (
        <button
          key={l}
          onClick={() => setLang(l)}
          className={cn(
            'relative h-7 w-9 rounded-full uppercase transition-colors',
            lang === l ? (dark ? 'text-ink' : 'text-white') : dark ? 'text-white/70 hover:text-white' : 'text-ink/50 hover:text-ink',
          )}
          aria-label={l === 'ru' ? 'Русский' : 'Тоҷикӣ'}
        >
          {lang === l && <motion.span layoutId="lang-pill" className={cn('absolute inset-0 rounded-full', dark ? 'bg-white' : 'bg-ink')} transition={{ type: 'spring', bounce: 0.2, duration: 0.4 }} />}
          <span className="relative">{l === 'ru' ? 'RU' : 'TJ'}</span>
        </button>
      ))}
    </div>
  );
}

function Footer() {
  const openLead = useLeadModal();
  const { data: building } = useMainBuilding();
  return (
    <footer className="relative overflow-hidden bg-ink text-white">
      <div className="pointer-events-none absolute -right-40 -top-40 size-[520px] rounded-full bg-gold/10 blur-3xl" />
      <div className="container-x relative py-16">
        <div className="flex flex-col justify-between gap-10 border-b border-white/10 pb-12 lg:flex-row lg:items-end">
          <div>
            <p className="eyebrow text-gold">Sitora Residence</p>
            <h2 className="display mt-3 max-w-xl text-4xl leading-tight sm:text-5xl">
              Найдите квартиру, <em className="text-gold">в которой</em> хочется жить
            </h2>
          </div>
          <Button variant="gold" size="lg" iconRight={<ArrowUpRight className="size-4" />} onClick={() => openLead()}>
            Получить консультацию
          </Button>
        </div>
        <div className="grid gap-10 py-12 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <Logo light />
            <p className="mt-4 max-w-xs text-sm text-white/55">{building?.address ?? 'г. Душанбе'}</p>
          </div>
          <FooterCol title="Покупателям">
            <FooterLink to={building ? `/building/${building.id}` : '/apartments'}>Выбор на 3D-модели</FooterLink>
            <FooterLink to="/apartments">Все квартиры</FooterLink>
            <FooterLink to="/cabinet">Личный кабинет</FooterLink>
          </FooterCol>
          <FooterCol title="Компания">
            <FooterLink to="/contacts">Контакты</FooterLink>
            <FooterLink to="/crm">Вход для сотрудников</FooterLink>
          </FooterCol>
          <FooterCol title="Связаться">
            <a href={`tel:${CONTACT_PHONE.replace(/\s/g, '')}`} className="link-underline text-sm tabular text-white/75 hover:text-white">
              {CONTACT_PHONE}
            </a>
            <span className="text-sm text-white/55">{contacts.hours}</span>
          </FooterCol>
        </div>
        <div className="flex flex-col justify-between gap-2 border-t border-white/10 pt-6 text-xs text-white/40 sm:flex-row">
          <span>© {new Date().getFullYear()} Sitora Residence</span>
          <span>Цены и наличие обновляются в реальном времени</span>
        </div>
      </div>
    </footer>
  );
}

function FooterCol({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-3">
      <p className="text-xs font-semibold uppercase tracking-[0.2em] text-white/40">{title}</p>
      {children}
    </div>
  );
}

function FooterLink({ to, children }: { to: string; children: React.ReactNode }) {
  return (
    <Link to={to} className="link-underline w-fit text-sm text-white/75 transition-colors hover:text-white">
      {children}
    </Link>
  );
}
