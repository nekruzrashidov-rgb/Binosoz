import { Link } from 'react-router';
import { motion } from 'motion/react';
import { useQuery } from '@tanstack/react-query';
import { ArrowUpRight, CalendarClock, ClipboardList, Coins, Hourglass, KanbanSquare, Plus } from 'lucide-react';
import { crm } from '@/api/endpoints';
import type { ApartmentStatus, DealStage } from '@/api/types';
import { ButtonLink, Card, CardHeader, ErrorState, PageHeader, Skeleton, StatCard } from '@/components/ui';
import { apartmentStatusLabel, dealStageLabel, moneyShort } from '@/lib/format';
import { ROLES } from '../access';
import { Guard } from '../Guard';

const STAGES: DealStage[] = ['Lead', 'Showing', 'Booking', 'Contract', 'Payment', 'Completed', 'Cancelled'];
const STATUS_COLORS: Record<ApartmentStatus, string> = { Free: '#2f9e6e', Booked: '#d99a2b', Sold: '#9aa0aa', OnRequest: '#4a74d9' };

export default function Dashboard() {
  return (
    <Guard roles={ROLES.sales}>
      <DashboardContent />
    </Guard>
  );
}

function DashboardContent() {
  const summary = useQuery({ queryKey: ['crm', 'summary'], queryFn: crm.summary, refetchInterval: 60_000 });
  const s = summary.data;
  const today = new Date().toLocaleDateString('ru-RU', { weekday: 'long', day: 'numeric', month: 'long' });

  return (
    <>
      <PageHeader
        eyebrow={today}
        title="Обзор продаж"
        subtitle="Данные обновляются каждую минуту"
        actions={
          <>
            <ButtonLink to="/crm/pipeline" variant="secondary" icon={<KanbanSquare className="size-4" />}>
              Воронка
            </ButtonLink>
            <ButtonLink to="/crm/deals?new=1" icon={<Plus className="size-4" />}>
              Новая сделка
            </ButtonLink>
          </>
        }
      />

      {summary.error ? (
        <ErrorState error={summary.error} onRetry={() => summary.refetch()} />
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {!s ? (
              Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-[132px] rounded-2xl" />)
            ) : (
              <>
                <StatCard tone="dark" label="Новые заявки" value={s.newLeads} hint={`${s.leadsInProgress} в работе`} icon={<ClipboardList className="size-4" />} />
                <StatCard label="Активные брони" value={s.activeBookings} hint="бронь держится 48 часов" icon={<CalendarClock className="size-4" />} delay={0.05} />
                <StatCard
                  tone={s.bookingsExpiringIn24Hours > 0 ? 'gold' : 'default'}
                  label="Истекают за сутки"
                  value={s.bookingsExpiringIn24Hours}
                  hint={s.bookingsExpiringIn24Hours > 0 ? 'продлите или переведите в сделку' : 'всё под контролем'}
                  icon={<Hourglass className="size-4" />}
                  delay={0.1}
                />
                <StatCard
                  label="Продано"
                  value={s.soldAmount.length ? s.soldAmount.map((m) => moneyShort(m.amount, m.currency)).join(' + ') : '0'}
                  hint="сумма завершённых и оплачиваемых сделок"
                  icon={<Coins className="size-4" />}
                  delay={0.15}
                />
              </>
            )}
          </div>

          <div className="mt-6 grid gap-6 xl:grid-cols-[1.4fr_1fr]">
            <Card>
              <CardHeader title="Сделки по этапам" action={<Link to="/crm/deals" className="text-sm text-muted hover:text-ink">Все сделки →</Link>} />
              <div className="flex flex-col gap-3 p-5">
                {!s
                  ? Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-8" />)
                  : (() => {
                      const max = Math.max(1, ...STAGES.map((st) => s.dealsByStage[st] ?? 0));
                      return STAGES.map((st, i) => {
                        const v = s.dealsByStage[st] ?? 0;
                        return (
                          <Link key={st} to={`/crm/deals?stage=${st}`} className="group grid grid-cols-[96px_1fr_40px] items-center gap-3">
                            <span className="text-sm text-muted group-hover:text-ink">{dealStageLabel[st]}</span>
                            <span className="h-8 overflow-hidden rounded-lg bg-paper">
                              <motion.span
                                className={st === 'Cancelled' ? 'block h-full rounded-lg bg-danger/30' : 'block h-full rounded-lg bg-ink transition-colors group-hover:bg-gold'}
                                initial={{ width: 0 }}
                                animate={{ width: `${(v / max) * 100}%` }}
                                transition={{ delay: 0.1 + i * 0.05, duration: 0.7, ease: [0.22, 1, 0.36, 1] }}
                              />
                            </span>
                            <span className="text-right font-semibold tabular">{v}</span>
                          </Link>
                        );
                      });
                    })()}
              </div>
            </Card>

            <Card>
              <CardHeader title="Квартиры" action={<Link to="/crm/apartments" className="text-sm text-muted hover:text-ink">Шахматка →</Link>} />
              <div className="flex flex-col items-center gap-6 p-6 sm:flex-row">
                {s ? <Donut data={s.apartmentsByStatus} /> : <Skeleton className="size-44 rounded-full" />}
                <div className="flex w-full flex-col gap-2">
                  {(['Free', 'Booked', 'Sold', 'OnRequest'] as const).map((st) => (
                    <div key={st} className="flex items-center justify-between rounded-xl px-3 py-2 transition-colors hover:bg-paper">
                      <span className="flex items-center gap-2.5 text-sm">
                        <span className="size-2.5 rounded-full" style={{ background: STATUS_COLORS[st] }} />
                        {apartmentStatusLabel[st]}
                      </span>
                      <span className="font-semibold tabular">{s?.apartmentsByStatus[st] ?? '—'}</span>
                    </div>
                  ))}
                </div>
              </div>
            </Card>
          </div>

          <div className="mt-6 grid gap-4 md:grid-cols-3">
            {[
              { to: '/crm/leads', title: 'Разобрать заявки', text: 'Новые обращения с сайта и звонков' },
              { to: '/crm/customers', title: 'Клиенты', text: 'Поиск, карточки, паспортные данные' },
              { to: '/crm/apartments', title: 'Забронировать квартиру', text: 'Выбор свободной квартиры для клиента' },
            ].map((l, i) => (
              <motion.div key={l.to} initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.2 + i * 0.05 }}>
                <Link to={l.to} className="group flex items-center justify-between rounded-2xl border border-line/80 bg-white p-5 transition-all duration-300 hover:-translate-y-0.5 hover:border-ink hover:bg-ink hover:text-white hover:shadow-lift">
                  <span>
                    <span className="block font-semibold">{l.title}</span>
                    <span className="mt-0.5 block text-sm text-muted transition-colors group-hover:text-white/55">{l.text}</span>
                  </span>
                  <ArrowUpRight className="size-5 transition-transform group-hover:rotate-45 group-hover:text-gold" />
                </Link>
              </motion.div>
            ))}
          </div>
        </>
      )}
    </>
  );
}

function Donut({ data }: { data: Partial<Record<ApartmentStatus, number>> }) {
  const entries = (['Free', 'Booked', 'Sold', 'OnRequest'] as const).map((k) => [k, data[k] ?? 0] as const);
  const total = entries.reduce((s, [, v]) => s + v, 0) || 1;
  const r = 70;
  const c = 2 * Math.PI * r;
  let offset = 0;
  return (
    <div className="relative size-44 shrink-0">
      <svg viewBox="0 0 180 180" className="-rotate-90">
        <circle cx="90" cy="90" r={r} fill="none" stroke="#f0ece5" strokeWidth="18" />
        {entries.map(([k, v], i) => {
          const len = (v / total) * c;
          const el = (
            <motion.circle
              key={k}
              cx="90"
              cy="90"
              r={r}
              fill="none"
              stroke={STATUS_COLORS[k]}
              strokeWidth="18"
              strokeDasharray={`${len} ${c - len}`}
              initial={{ strokeDashoffset: c }}
              animate={{ strokeDashoffset: -offset }}
              transition={{ delay: 0.1 + i * 0.1, duration: 0.9, ease: [0.22, 1, 0.36, 1] }}
            />
          );
          offset += len;
          return el;
        })}
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="display text-4xl tabular">{data.Free ?? 0}</span>
        <span className="text-xs text-muted">свободно</span>
      </div>
    </div>
  );
}
