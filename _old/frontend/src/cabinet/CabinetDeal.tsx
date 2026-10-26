import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { AnimatePresence, motion } from 'motion/react';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, CalendarRange, FileText, HardHat, MessageCircle } from 'lucide-react';
import { ApiError } from '@/api/client';
import { cabinet, catalog } from '@/api/endpoints';
import { EmptyState, ErrorState, Skeleton, Tabs } from '@/components/ui';
import { area, dateShort, money, roomsLabel } from '@/lib/format';
import { ChatPanel } from '@/shared/ChatPanel';
import { ConstructionView } from '@/shared/ConstructionView';
import { DocumentsList } from '@/shared/DocumentsList';
import { ScheduleView } from '@/shared/ScheduleView';
import { StageTimeline } from '@/shared/StageTimeline';

type Tab = 'schedule' | 'documents' | 'chat' | 'construction';

export default function CabinetDeal() {
  const { id = '' } = useParams();
  // Отдельного запроса сделки для покупателя нет — берём её из списка своих сделок.
  const deals = useQuery({ queryKey: ['cabinet', 'deals'], queryFn: cabinet.deals });
  const deal = deals.data?.find((d) => d.id === id);
  const apartment = useQuery({ queryKey: ['apartment', deal?.apartmentId], queryFn: () => catalog.apartment(deal!.apartmentId), enabled: !!deal });
  const [tab, setTab] = useState<Tab>('schedule');

  if (deals.error) return <ErrorState error={deals.error} onRetry={() => deals.refetch()} />;
  if (deals.data && !deal) return <EmptyState title="Сделка не найдена" action={<Link to="/cabinet" className="text-sm font-medium text-gold-2">← К моим сделкам</Link>} />;

  const a = apartment.data;

  return (
    <>
      <Link to="/cabinet" className="inline-flex items-center gap-1.5 text-sm text-muted hover:text-ink">
        <ArrowLeft className="size-4" /> Мои сделки
      </Link>

      <motion.section initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="relative mt-5 overflow-hidden rounded-[28px] bg-ink p-7 text-white sm:p-10">
        <div className="absolute -right-24 -top-24 size-80 rounded-full bg-gold/15 blur-3xl" />
        <div className="relative flex flex-wrap items-end justify-between gap-6">
          <div>
            <p className="text-sm text-white/50">{deal?.buildingName ?? <Skeleton className="h-4 w-32 bg-white/10" />}</p>
            <h1 className="display mt-2 text-5xl sm:text-6xl">
              Квартира <span className="text-gold">{deal?.apartmentNumber ?? '…'}</span>
            </h1>
            {a && (
              <p className="mt-2 text-white/55">
                {roomsLabel(a.rooms)} · {area(a.area)} · {a.floorNumber} этаж
              </p>
            )}
          </div>
          {deal && (
            <div className="text-right">
              <p className="text-sm text-white/50">Стоимость по договору</p>
              <p className="display text-4xl tabular">{money(deal.finalPrice, deal.currency)}</p>
              {deal.discount > 0 && <p className="text-sm text-gold tabular">скидка {money(deal.discount, deal.currency)}</p>}
              <p className="mt-1 text-xs text-white/40">с {dateShort(deal.createdAt)}</p>
            </div>
          )}
        </div>
        {deal && (
          <div className="relative mt-10">
            <StageTimeline stage={deal.stage} dark />
          </div>
        )}
      </motion.section>

      <Tabs
        className="mt-8"
        value={tab}
        onChange={setTab}
        tabs={[
          { value: 'schedule', label: <><CalendarRange className="size-4" /> Платежи</> },
          { value: 'documents', label: <><FileText className="size-4" /> Документы</> },
          { value: 'chat', label: <><MessageCircle className="size-4" /> Чат</> },
          { value: 'construction', label: <><HardHat className="size-4" /> Стройка</> },
        ]}
      />

      <AnimatePresence mode="wait">
        <motion.div key={tab} initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }} transition={{ duration: 0.2 }} className="mt-6">
          {tab === 'schedule' && <ScheduleTab dealId={id} />}
          {tab === 'documents' && <DocumentsTab dealId={id} />}
          {tab === 'chat' && <ChatPanel dealId={id} realm="customer" />}
          {tab === 'construction' && (a ? <ConstructionTab buildingId={a.buildingId} /> : <Skeleton className="h-64" />)}
        </motion.div>
      </AnimatePresence>
    </>
  );
}

function ScheduleTab({ dealId }: { dealId: string }) {
  const q = useQuery({ queryKey: ['cabinet', 'schedule', dealId], queryFn: () => cabinet.schedule(dealId), retry: false });
  if (q.isLoading) return <Skeleton className="h-96 rounded-2xl" />;
  if (q.error instanceof ApiError && q.error.status === 404)
    return <EmptyState icon={<CalendarRange className="size-6" />} title="График ещё не составлен" text="Менеджер составит график платежей после подписания договора." />;
  if (q.error) return <ErrorState error={q.error} onRetry={() => q.refetch()} />;
  return <ScheduleView schedule={q.data!} realm="customer" />;
}

function DocumentsTab({ dealId }: { dealId: string }) {
  const q = useQuery({ queryKey: ['cabinet', 'documents', dealId], queryFn: () => cabinet.documents(dealId) });
  if (q.isLoading) return <div className="grid gap-3 sm:grid-cols-2"><Skeleton className="h-24" /><Skeleton className="h-24" /></div>;
  if (q.error) return <ErrorState error={q.error} onRetry={() => q.refetch()} />;
  return <DocumentsList documents={q.data!} realm="customer" />;
}

function ConstructionTab({ buildingId }: { buildingId: string }) {
  const q = useQuery({ queryKey: ['cabinet', 'stages', buildingId], queryFn: () => cabinet.stages(buildingId) });
  if (q.isLoading) return <Skeleton className="h-96 rounded-2xl" />;
  if (q.error) return <ErrorState error={q.error} onRetry={() => q.refetch()} />;
  return <ConstructionView progress={q.data!} />;
}
