import { lazy, Suspense, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { motion, useScroll, useTransform } from 'motion/react';
import { ArrowRight, ArrowUpRight, CalendarCheck, FileText, KeyRound, MessagesSquare, MousePointerClick, ShieldCheck, Sparkles, Timer, Wallet } from 'lucide-react';
import { Button, ButtonLink, Reveal, Skeleton } from '@/components/ui';
import { area, money, moneyShort, roomsLabel } from '@/lib/format';
import { cn } from '@/lib/cn';
import { useLeadModal } from './LeadModal';
import { summarizeByRooms, useAllApartments, useMainBuilding } from './hooks';

const Building3D = lazy(() => import('./Building3D'));

export function HomePage() {
  return (
    <>
      <Hero />
      <TypesSection />
      <StepsSection />
      <CalculatorSection />
      <CabinetSection />
    </>
  );
}

function Hero() {
  const { data: building, isLoading } = useMainBuilding();
  const { data: apartments, floors } = useAllApartments(building?.id);
  const navigate = useNavigate();
  const openLead = useLeadModal();
  const { scrollY } = useScroll();
  // Параллакс текста — только на широком экране: на телефоне модель стоит под текстом, и он бы на неё наезжал.
  const wide = useWide();
  const textY = useTransform(scrollY, [0, 600], wide ? [0, 120] : [0, 0]);
  const fade = useTransform(scrollY, [0, 500], wide ? [1, 0] : [1, 1]);

  const minPrice = useMemo(() => {
    const free = apartments?.filter((a) => a.status === 'Free') ?? [];
    return free.length ? Math.min(...free.map((a) => a.price)) : null;
  }, [apartments]);

  const name = building?.name ?? 'Sitora Residence';
  const words = name.split(' ');

  return (
    <section className="relative overflow-hidden bg-ink text-white">
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute -left-40 top-20 size-[620px] rounded-full bg-gold/[0.09] blur-3xl" />
        <div className="absolute bottom-0 right-0 size-[520px] rounded-full bg-[#4a74d9]/[0.08] blur-3xl" />
        <div className="absolute inset-0 bg-[radial-gradient(rgb(255_255_255/0.06)_1px,transparent_1px)] [background-size:28px_28px] [mask-image:radial-gradient(ellipse_at_center,black,transparent_75%)]" />
      </div>

      <div className="container-x relative grid min-h-[100svh] items-center gap-6 pb-10 pt-28 lg:grid-cols-[1.05fr_1fr] lg:pt-20">
        <motion.div style={{ y: textY, opacity: fade }} className="relative z-10">
          <motion.p initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }} className="eyebrow flex items-center gap-2 text-gold">
            <Sparkles className="size-3.5" /> {building?.address ?? 'Душанбе'} · жилой комплекс
          </motion.p>
          <h1 className="display mt-5 text-[52px] leading-[0.95] sm:text-7xl xl:text-[88px]">
            {words.map((w, i) => (
              <motion.span
                key={i}
                className={cn('mr-[0.22em] inline-block', i === words.length - 1 && 'italic text-gold')}
                initial={{ opacity: 0, y: 40, filter: 'blur(8px)' }}
                animate={{ opacity: 1, y: 0, filter: 'blur(0px)' }}
                transition={{ delay: 0.2 + i * 0.12, duration: 0.8, ease: [0.22, 1, 0.36, 1] }}
              >
                {w}
              </motion.span>
            ))}
          </h1>
          <motion.p
            initial={{ opacity: 0, y: 14 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.55 }}
            className="mt-6 max-w-lg text-lg leading-relaxed text-white/65"
          >
            {building?.description ?? 'Выберите квартиру прямо на 3D-модели дома и забронируйте её онлайн на 48 часов — без звонков и очередей.'}
          </motion.p>
          <motion.div initial={{ opacity: 0, y: 14 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.7 }} className="mt-9 flex flex-wrap gap-3">
            <ButtonLink to={building ? `/building/${building.id}` : '/apartments'} variant="gold" size="lg" iconRight={<ArrowRight className="size-4" />}>
              Выбрать квартиру
            </ButtonLink>
            <Button variant="outline-light" size="lg" onClick={() => openLead()}>
              Консультация
            </Button>
          </motion.div>

          <motion.dl
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 0.9 }}
            className="mt-14 grid max-w-lg grid-cols-3 gap-6 border-t border-white/10 pt-6"
          >
            <HeroStat label="Свободно" value={isLoading || !building ? null : building.freeApartments} suffix={`из ${building?.totalApartments ?? '—'}`} />
            <HeroStat label="Этажей" value={building?.floorsCount ?? null} />
            <HeroStat label="Цена от" value={minPrice === null ? null : moneyShort(minPrice).replace(' сом.', '')} suffix="сом." />
          </motion.dl>
        </motion.div>

        <motion.div
          initial={{ opacity: 0, scale: 0.94 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ delay: 0.3, duration: 1.1, ease: [0.22, 1, 0.36, 1] }}
          className="relative h-[460px] sm:h-[560px] lg:h-[720px]"
        >
          <Suspense fallback={<BuildingFallback />}>
            {floors && floors.length > 0 ? (
              <Building3D floors={floors} onSelect={(f) => navigate(`/floors/${f.id}`)} />
            ) : (
              <BuildingFallback />
            )}
          </Suspense>
          <div className="pointer-events-none absolute bottom-6 left-1/2 flex -translate-x-1/2 items-center gap-2 rounded-full border border-white/15 bg-white/5 px-4 py-2 text-xs text-white/70 backdrop-blur">
            <MousePointerClick className="size-3.5 text-gold" /> Наведите на этаж и нажмите, чтобы открыть план
          </div>
        </motion.div>
      </div>
    </section>
  );
}

function useWide() {
  const query = '(min-width: 1024px)';
  const [wide, setWide] = useState(() => window.matchMedia(query).matches);
  useEffect(() => {
    const m = window.matchMedia(query);
    const on = () => setWide(m.matches);
    m.addEventListener('change', on);
    return () => m.removeEventListener('change', on);
  }, []);
  return wide;
}

function HeroStat({ label, value, suffix }: { label: string; value: string | number | null; suffix?: string }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-[0.18em] text-white/45">{label}</dt>
      <dd className="mt-2 font-display text-3xl tabular">
        {value === null ? <span className="inline-block h-7 w-16 animate-pulse rounded bg-white/10" /> : value}
        {suffix && value !== null && <span className="ml-1.5 font-sans text-sm text-white/45">{suffix}</span>}
      </dd>
    </div>
  );
}

function BuildingFallback() {
  return (
    <div className="flex h-full items-center justify-center">
      <div className="flex flex-col items-center gap-1.5">
        {Array.from({ length: 12 }).map((_, i) => (
          <motion.div
            key={i}
            className="h-4 w-56 rounded-sm bg-white/[0.07]"
            animate={{ opacity: [0.3, 0.8, 0.3] }}
            transition={{ duration: 1.6, repeat: Infinity, delay: i * 0.08 }}
          />
        ))}
      </div>
    </div>
  );
}

function TypesSection() {
  const { data: building } = useMainBuilding();
  const { data: apartments, isLoading } = useAllApartments(building?.id);
  const types = useMemo(() => summarizeByRooms(apartments ?? []), [apartments]);

  return (
    <section className="container-x py-24 sm:py-32">
      <Reveal className="flex flex-col justify-between gap-6 md:flex-row md:items-end">
        <div>
          <p className="eyebrow">Планировки</p>
          <h2 className="display mt-3 text-4xl sm:text-5xl">Квартиры для любого сценария жизни</h2>
        </div>
        <Link to="/apartments" className="group inline-flex items-center gap-2 text-sm font-medium">
          <span className="link-underline">Все квартиры с фильтрами</span>
          <ArrowUpRight className="size-4 transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5" />
        </Link>
      </Reveal>

      <div className="mt-12 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {isLoading
          ? Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-[300px] rounded-2xl" />)
          : types.map((t, i) => (
              <Reveal key={t.rooms} delay={i * 0.08}>
                <Link
                  to={`/apartments?rooms=${t.rooms}`}
                  className="group relative flex h-[300px] flex-col overflow-hidden rounded-2xl border border-line/80 bg-white p-6 transition-all duration-500 hover:-translate-y-1 hover:border-ink hover:bg-ink hover:text-white hover:shadow-lift"
                >
                  <div className="flex items-start justify-between">
                    <span className="font-display text-6xl leading-none text-gold transition-transform duration-500 group-hover:scale-110">
                      {t.rooms === 0 ? 'S' : t.rooms}
                    </span>
                    <span className="flex size-10 items-center justify-center rounded-full border border-line transition-all duration-500 group-hover:rotate-45 group-hover:border-white/20 group-hover:bg-gold group-hover:text-ink">
                      <ArrowUpRight className="size-4" />
                    </span>
                  </div>
                  <MiniPlan rooms={t.rooms} />
                  <div className="mt-auto">
                    <p className="text-lg font-semibold">{roomsLabel(t.rooms)}</p>
                    <p className="mt-1 text-sm text-muted transition-colors group-hover:text-white/60">
                      {t.minArea === t.maxArea ? area(t.minArea) : `${t.minArea}–${area(t.maxArea)}`} · свободно {t.free}
                    </p>
                    <p className="mt-3 text-sm">
                      {t.minPrice !== null ? (
                        <>
                          от <span className="text-base font-semibold">{money(t.minPrice)}</span>
                        </>
                      ) : (
                        <span className="text-muted">Нет в продаже</span>
                      )}
                    </p>
                  </div>
                </Link>
              </Reveal>
            ))}
      </div>
    </section>
  );
}

// Схематичная планировка: число комнат — число прямоугольников.
function MiniPlan({ rooms }: { rooms: number }) {
  const cells = Math.max(1, rooms);
  return (
    <div className="pointer-events-none absolute right-6 top-24 grid h-24 w-28 gap-1 opacity-20 transition-opacity duration-500 group-hover:opacity-40" style={{ gridTemplateColumns: `repeat(${Math.min(cells, 2)}, 1fr)` }}>
      {Array.from({ length: cells + 1 }).map((_, i) => (
        <div key={i} className={cn('rounded-sm border-2 border-current', i === 0 && cells > 1 && 'col-span-2')} />
      ))}
    </div>
  );
}

const steps = [
  { icon: MousePointerClick, title: 'Выберите квартиру', text: 'На 3D-модели дома или в каталоге с фильтрами. Цены и статусы — в реальном времени.' },
  { icon: Timer, title: 'Забронируйте онлайн', text: 'Бронь держится 48 часов. Квартира сразу пропадает из продажи для других.' },
  { icon: FileText, title: 'Подпишите договор', text: 'В сомони или в долларах. Менеджер свяжется и подготовит документы.' },
  { icon: Wallet, title: 'Платите по графику', text: 'Рассрочка по графику. Платежи и документы — в личном кабинете.' },
];

function StepsSection() {
  const { scrollYProgress } = useScroll();
  const line = useTransform(scrollYProgress, [0.25, 0.55], ['0%', '100%']);
  return (
    <section className="bg-paper-2/60 py-24 sm:py-32">
      <div className="container-x">
        <Reveal className="max-w-2xl">
          <p className="eyebrow">Как купить</p>
          <h2 className="display mt-3 text-4xl sm:text-5xl">От выбора до ключей — четыре шага</h2>
        </Reveal>
        <div className="relative mt-16">
          <div className="absolute left-0 right-0 top-7 hidden h-px bg-line lg:block">
            <motion.div style={{ width: line }} className="h-full bg-gold" />
          </div>
          <div className="grid gap-10 lg:grid-cols-4">
            {steps.map((s, i) => (
              <Reveal key={s.title} delay={i * 0.1} className="group relative">
                <div className="relative z-10 flex size-14 items-center justify-center rounded-2xl border border-line bg-white shadow-soft transition-all duration-300 group-hover:-translate-y-1 group-hover:border-gold group-hover:bg-ink group-hover:text-gold">
                  <s.icon className="size-6" />
                </div>
                <p className="mt-6 font-display text-sm text-gold-2">0{i + 1}</p>
                <h3 className="mt-1 text-xl font-semibold">{s.title}</h3>
                <p className="mt-2 text-[15px] leading-relaxed text-muted">{s.text}</p>
              </Reveal>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}

function CalculatorSection() {
  const [price, setPrice] = useState(650_000);
  const [down, setDown] = useState(30);
  const [months, setMonths] = useState(36);
  const openLead = useLeadModal();
  // То же правило, что на сервере: остаток делится на месяцы с округлением до целого.
  const downAmount = Math.round((price * down) / 100);
  const monthly = Math.round((price - downAmount) / months);

  return (
    <section className="container-x py-24 sm:py-32">
      <div className="grid overflow-hidden rounded-[28px] bg-ink text-white lg:grid-cols-[1.1fr_1fr]">
        <div className="p-8 sm:p-12">
          <Reveal>
            <p className="eyebrow text-gold">Рассрочка</p>
            <h2 className="display mt-3 text-4xl sm:text-5xl">Посчитайте ежемесячный платёж</h2>
            <p className="mt-4 max-w-md text-white/55">Точный график составит менеджер. Последний платёж включает остаток от округлений.</p>
          </Reveal>
          <div className="mt-10 flex flex-col gap-8">
            <Slider label="Стоимость квартиры" value={price} min={250_000} max={2_000_000} step={10_000} onChange={setPrice} format={(v) => money(v)} />
            <Slider label="Первоначальный взнос" value={down} min={10} max={90} step={5} onChange={setDown} format={(v) => `${v}% · ${money(Math.round((price * v) / 100))}`} />
            <Slider label="Срок" value={months} min={6} max={120} step={6} onChange={setMonths} format={(v) => `${v} мес.`} />
          </div>
        </div>
        <div className="relative flex flex-col justify-between bg-gradient-to-br from-ink-3 to-ink p-8 sm:p-12">
          <div className="absolute right-0 top-0 size-72 rounded-full bg-gold/15 blur-3xl" />
          <div className="relative">
            <p className="text-sm text-white/50">Ежемесячный платёж</p>
            <motion.p key={monthly} initial={{ opacity: 0.4, y: 6 }} animate={{ opacity: 1, y: 0 }} className="display mt-2 text-6xl tabular text-gold sm:text-7xl">
              {money(monthly)}
            </motion.p>
            <div className="mt-8 grid grid-cols-2 gap-4 border-t border-white/10 pt-6 text-sm">
              <div>
                <p className="text-white/45">Взнос</p>
                <p className="mt-1 text-lg font-semibold tabular">{money(downAmount)}</p>
              </div>
              <div>
                <p className="text-white/45">В рассрочку</p>
                <p className="mt-1 text-lg font-semibold tabular">{money(price - downAmount)}</p>
              </div>
            </div>
          </div>
          <Button variant="gold" size="lg" className="relative mt-10" onClick={() => openLead({ title: `Рассрочка: ${money(monthly)} в месяц на ${months} мес.` })}>
            Обсудить условия
          </Button>
        </div>
      </div>
    </section>
  );
}

function Slider({ label, value, min, max, step, onChange, format }: { label: string; value: number; min: number; max: number; step: number; onChange: (v: number) => void; format: (v: number) => string }) {
  const pct = ((value - min) / (max - min)) * 100;
  return (
    <label className="block">
      <div className="mb-3 flex items-baseline justify-between gap-4">
        <span className="text-sm text-white/55">{label}</span>
        <span className="text-right text-base font-semibold tabular">{format(value)}</span>
      </div>
      <input
        type="range"
        min={min}
        max={max}
        step={step}
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
        className="h-1.5 w-full cursor-pointer appearance-none rounded-full outline-none [&::-moz-range-thumb]:size-5 [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:border-0 [&::-moz-range-thumb]:bg-gold [&::-webkit-slider-thumb]:size-5 [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-gold [&::-webkit-slider-thumb]:shadow-[0_0_0_6px_rgb(201_164_106/0.18)] [&::-webkit-slider-thumb]:transition-transform hover:[&::-webkit-slider-thumb]:scale-125"
        style={{ background: `linear-gradient(to right, var(--color-gold) ${pct}%, rgb(255 255 255 / 0.12) ${pct}%)` }}
      />
    </label>
  );
}

const cabinetFeatures = [
  { icon: CalendarCheck, title: 'График платежей', text: 'Что оплачено, что впереди, есть ли просрочка' },
  { icon: FileText, title: 'Документы', text: 'Договор, акты и квитанции в одном месте' },
  { icon: MessagesSquare, title: 'Чат с менеджером', text: 'Ответ без звонков, вся переписка сохраняется' },
  { icon: ShieldCheck, title: 'Ход стройки', text: 'Фото и проценты готовности по этапам' },
];

function CabinetSection() {
  return (
    <section className="bg-paper-2/60 py-24 sm:py-32">
      <div className="container-x grid items-center gap-14 lg:grid-cols-2">
        <Reveal>
          <p className="eyebrow">Личный кабинет</p>
          <h2 className="display mt-3 text-4xl sm:text-5xl">Всё о вашей покупке — в телефоне</h2>
          <p className="mt-4 max-w-md text-muted">Вход по номеру телефона и SMS-коду. Платежи и документы появляются в кабинете сразу, как только их внесёт офис.</p>
          <div className="mt-10 grid gap-3 sm:grid-cols-2">
            {cabinetFeatures.map((f, i) => (
              <Reveal key={f.title} delay={i * 0.06}>
                <div className="group flex h-full gap-4 rounded-2xl border border-line/80 bg-white p-5 transition-all duration-300 hover:-translate-y-0.5 hover:shadow-lift">
                  <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-gold-soft text-gold-2 transition-colors duration-300 group-hover:bg-ink group-hover:text-gold">
                    <f.icon className="size-5" />
                  </span>
                  <div>
                    <p className="font-semibold">{f.title}</p>
                    <p className="mt-0.5 text-sm text-muted">{f.text}</p>
                  </div>
                </div>
              </Reveal>
            ))}
          </div>
          <ButtonLink to="/cabinet" className="mt-10" size="lg" icon={<KeyRound className="size-4" />}>
            Войти в кабинет
          </ButtonLink>
        </Reveal>

        <Reveal delay={0.15}>
          <PhoneMock />
        </Reveal>
      </div>
    </section>
  );
}

function PhoneMock() {
  const rows = [
    { d: '15 ноя', s: 'Оплачено', ok: true },
    { d: '15 дек', s: 'Оплачено', ok: true },
    { d: '15 янв', s: 'Ожидается', ok: false },
    { d: '15 фев', s: 'Ожидается', ok: false },
  ];
  return (
    <div className="relative mx-auto w-full max-w-[340px] animate-float">
      <div className="absolute -inset-10 rounded-full bg-gold/20 blur-3xl" />
      <div className="relative rounded-[44px] border-[10px] border-ink bg-ink shadow-lift">
        <div className="overflow-hidden rounded-[34px] bg-paper">
          <div className="bg-ink px-6 pb-8 pt-10 text-white">
            <p className="text-xs text-white/50">Квартира 1203 · 12 этаж</p>
            <p className="mt-1 font-display text-3xl">Оплачено 46%</p>
            <div className="mt-4 h-1.5 overflow-hidden rounded-full bg-white/10">
              <motion.div className="h-full rounded-full bg-gold" initial={{ width: 0 }} whileInView={{ width: '46%' }} viewport={{ once: true }} transition={{ duration: 1.4, delay: 0.3 }} />
            </div>
          </div>
          <div className="-mt-4 rounded-t-3xl bg-paper px-5 pb-8 pt-5">
            <p className="text-xs font-semibold uppercase tracking-widest text-muted">График</p>
            {rows.map((r, i) => (
              <motion.div
                key={r.d}
                initial={{ opacity: 0, x: 20 }}
                whileInView={{ opacity: 1, x: 0 }}
                viewport={{ once: true }}
                transition={{ delay: 0.4 + i * 0.1 }}
                className="mt-3 flex items-center justify-between rounded-2xl bg-white px-4 py-3 shadow-soft"
              >
                <div>
                  <p className="text-sm font-semibold">{r.d}</p>
                  <p className="text-xs text-muted">12 600 сом.</p>
                </div>
                <span className={cn('rounded-full px-2.5 py-1 text-[11px] font-medium', r.ok ? 'bg-free-soft text-free' : 'bg-paper-2 text-muted')}>{r.s}</span>
              </motion.div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
