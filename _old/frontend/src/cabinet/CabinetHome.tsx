import { Link } from 'react-router';
import { motion } from 'motion/react';
import { useQuery } from '@tanstack/react-query';
import { ArrowUpRight, Building2, Home } from 'lucide-react';
import { ApiError } from '@/api/client';
import { cabinet } from '@/api/endpoints';
import { useSession } from '@/api/session';
import type { DealResponse } from '@/api/types';
import { Badge, ButtonLink, EmptyState, ErrorState, ProgressBar, Skeleton } from '@/components/ui';
import { dealStageLabel, money } from '@/lib/format';

export default function CabinetHome() {
  const session = useSession('customer');
  const deals = useQuery({ queryKey: ['cabinet', 'deals'], queryFn: cabinet.deals });
  const firstName = session?.user.fullName.split(' ')[0] ?? '';
  const hour = new Date().getHours();
  const greeting = hour < 6 ? 'Доброй ночи' : hour < 12 ? 'Доброе утро' : hour < 18 ? 'Добрый день' : 'Добрый вечер';

  return (
    <>
      <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}>
        <p className="eyebrow">Личный кабинет</p>
        <h1 className="display mt-2 text-4xl sm:text-5xl">
          {greeting}, {firstName}
        </h1>
        <p className="mt-2 text-muted">Здесь всё о ваших покупках: платежи, документы и связь с менеджером.</p>
      </motion.div>

      <div className="mt-10">
        {deals.isLoading ? (
          <div className="grid gap-4 md:grid-cols-2">
            <Skeleton className="h-64 rounded-[24px]" />
            <Skeleton className="h-64 rounded-[24px]" />
          </div>
        ) : deals.error ? (
          <ErrorState error={deals.error} onRetry={() => deals.refetch()} />
        ) : deals.data!.length === 0 ? (
          <EmptyState
            icon={<Home className="size-6" />}
            title="Сделок пока нет"
            text="Когда менеджер оформит вашу покупку, она появится здесь."
            action={<ButtonLink to="/apartments">Посмотреть квартиры</ButtonLink>}
          />
        ) : (
          <div className="grid gap-5 md:grid-cols-2">
            {deals.data!.map((d, i) => (
              <DealCard key={d.id} deal={d} index={i} />
            ))}
          </div>
        )}
      </div>
    </>
  );
}

function DealCard({ deal, index }: { deal: DealResponse; index: number }) {
  // У сделки без графика сервер отвечает 404 — это нормальное состояние «график ещё не составлен».
  const schedule = useQuery({
    queryKey: ['cabinet', 'schedule', deal.id],
    queryFn: () => cabinet.schedule(deal.id),
    retry: false,
  });
  const noPlan = schedule.error instanceof ApiError && schedule.error.status === 404;
  const paidPct = schedule.data?.total ? Math.round((schedule.data.paid / schedule.data.total) * 100) : 0;

  return (
    <motion.div initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: index * 0.08 }}>
      <Link
        to={`/cabinet/deals/${deal.id}`}
        className="group relative flex h-full flex-col overflow-hidden rounded-[24px] bg-ink p-7 text-white transition-all duration-500 hover:-translate-y-1 hover:shadow-lift"
      >
        <div className="absolute -right-16 -top-16 size-56 rounded-full bg-gold/15 blur-3xl transition-opacity duration-500 group-hover:opacity-100" />
        <div className="relative flex items-start justify-between">
          <div>
            <p className="flex items-center gap-1.5 text-sm text-white/50">
              <Building2 className="size-4" /> {deal.buildingName}
            </p>
            <p className="display mt-2 text-5xl">
              Квартира <span className="text-gold">{deal.apartmentNumber}</span>
            </p>
          </div>
          <span className="flex size-11 items-center justify-center rounded-full border border-white/15 transition-all duration-500 group-hover:rotate-45 group-hover:border-gold group-hover:bg-gold group-hover:text-ink">
            <ArrowUpRight className="size-5" />
          </span>
        </div>
        <div className="relative mt-6 flex flex-wrap gap-2">
          <Badge tone={deal.stage === 'Cancelled' ? 'red' : 'gold'}>{dealStageLabel[deal.stage]}</Badge>
          <Badge className="bg-white/10 text-white/80">{deal.currency}</Badge>
        </div>
        <div className="relative mt-auto pt-8">
          {schedule.isLoading ? (
            <Skeleton className="h-10 bg-white/10" />
          ) : noPlan || !schedule.data ? (
            <div className="flex items-baseline justify-between">
              <span className="text-sm text-white/50">Стоимость</span>
              <span className="text-xl font-semibold tabular">{money(deal.finalPrice, deal.currency)}</span>
            </div>
          ) : (
            <>
              <div className="flex items-baseline justify-between text-sm">
                <span className="text-white/50">Оплачено {paidPct}%</span>
                <span className="font-semibold tabular">
                  {money(schedule.data.paid, deal.currency)} <span className="text-white/40">/ {money(schedule.data.total, deal.currency)}</span>
                </span>
              </div>
              <ProgressBar value={paidPct} className="mt-3 bg-white/10" />
              {schedule.data.overdue > 0 && <p className="mt-3 text-sm text-[#ff9b92]">Просрочено {money(schedule.data.overdue, deal.currency)}</p>}
            </>
          )}
        </div>
      </Link>
    </motion.div>
  );
}
