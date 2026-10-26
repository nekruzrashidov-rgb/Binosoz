import { useState } from 'react';
import { Link } from 'react-router';
import { AnimatePresence, motion } from 'motion/react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarPlus, Clock, GripVertical, Phone, RefreshCw, UserPlus, XCircle } from 'lucide-react';
import { crm } from '@/api/endpoints';
import type { DealStage, PipelineCard } from '@/api/types';
import { Badge, Button, ConfirmDialog, ErrorState, PageHeader, Skeleton, useToast } from '@/components/ui';
import { cn } from '@/lib/cn';
import { dealStageLabel, formatPhone, money, relative, timeLeft } from '@/lib/format';
import { ROLES } from '../access';
import { CustomerFormModal } from '../forms';
import { Guard } from '../Guard';

const ORDER: DealStage[] = ['Lead', 'Showing', 'Booking', 'Contract', 'Payment', 'Completed', 'Cancelled'];
const ACCENT: Record<DealStage, string> = {
  Lead: 'bg-request',
  Showing: 'bg-[#8a6fd1]',
  Booking: 'bg-booked',
  Contract: 'bg-gold',
  Payment: 'bg-ink',
  Completed: 'bg-free',
  Cancelled: 'bg-danger',
};

export default function Pipeline() {
  return (
    <Guard roles={ROLES.sales}>
      <PipelineBoard />
    </Guard>
  );
}

function PipelineBoard() {
  const queryClient = useQueryClient();
  const toast = useToast();
  const q = useQuery({ queryKey: ['crm', 'pipeline'], queryFn: crm.pipeline, refetchInterval: 30_000 });
  const [dragging, setDragging] = useState<PipelineCard | null>(null);
  const [over, setOver] = useState<DealStage | null>(null);
  const [backMove, setBackMove] = useState<{ card: PipelineCard; to: DealStage } | null>(null);
  const [cancelBooking, setCancelBooking] = useState<PipelineCard | null>(null);
  const [leadToCustomer, setLeadToCustomer] = useState<PipelineCard | null>(null);
  const [busy, setBusy] = useState(false);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['crm'] });

  // Перетаскивать можно только сделки: заявки и брони меняют этап через свои действия.
  const drop = (to: DealStage) => {
    const card = dragging;
    setDragging(null);
    setOver(null);
    if (!card || card.kind !== 'Deal') return;
    const from = q.data?.columns.find((c) => c.cards.some((x) => x.id === card.id))?.stage;
    if (!from || from === to) return;
    // Назад и в отмену — только с причиной.
    if (ORDER.indexOf(to) < ORDER.indexOf(from) || to === 'Cancelled') setBackMove({ card, to });
    else move(card, to);
  };

  const move = async (card: PipelineCard, to: DealStage, reason?: string) => {
    setBusy(true);
    try {
      await crm.changeStage(card.id, to, reason);
      toast.success(`${card.clientName}: этап «${dealStageLabel[to]}»`);
      setBackMove(null);
      refresh();
    } catch (e) {
      toast.error(e);
    } finally {
      setBusy(false);
    }
  };

  const extend = async (card: PipelineCard) => {
    try {
      await crm.extendBooking(card.id);
      toast.success('Бронь продлена на 48 часов');
      refresh();
    } catch (e) {
      toast.error(e);
    }
  };

  return (
    <>
      <PageHeader
        title="Воронка продаж"
        subtitle="Перетаскивайте сделки между этапами. Возврат назад и отмена — с причиной."
        actions={
          <Button variant="secondary" icon={<RefreshCw className={cn('size-4', q.isFetching && 'animate-spin')} />} onClick={() => q.refetch()}>
            Обновить
          </Button>
        }
      />

      {q.error ? (
        <ErrorState error={q.error} onRetry={() => q.refetch()} />
      ) : (
        <div className="scrollbar-thin -mx-4 flex gap-4 overflow-x-auto px-4 pb-6 sm:-mx-8 sm:px-8">
          {(q.data?.columns ?? ORDER.map((stage) => ({ stage, count: 0, cards: [] as PipelineCard[] }))).map((col) => (
            <div
              key={col.stage}
              onDragOver={(e) => {
                if (dragging?.kind === 'Deal') {
                  e.preventDefault();
                  setOver(col.stage);
                }
              }}
              onDragLeave={() => setOver((o) => (o === col.stage ? null : o))}
              onDrop={() => drop(col.stage)}
              className={cn(
                'flex w-[300px] shrink-0 flex-col rounded-2xl border bg-white/60 transition-all duration-200',
                over === col.stage ? 'border-gold bg-gold-soft/40 ring-4 ring-gold/15' : 'border-line/70',
              )}
            >
              <div className="flex items-center justify-between px-4 pb-2 pt-4">
                <span className="flex items-center gap-2 text-sm font-semibold">
                  <span className={cn('size-2 rounded-full', ACCENT[col.stage])} />
                  {dealStageLabel[col.stage]}
                </span>
                <span className="rounded-full bg-paper-2 px-2 py-0.5 text-xs font-semibold tabular text-muted">{col.count}</span>
              </div>
              <div className="scrollbar-thin flex max-h-[calc(100vh-230px)] min-h-[160px] flex-col gap-2 overflow-y-auto p-2">
                {q.isLoading ? (
                  Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-24 rounded-xl" />)
                ) : (
                  <AnimatePresence initial={false}>
                    {col.cards.map((card) => (
                      <Card
                        key={`${card.kind}-${card.id}`}
                        card={card}
                        dragging={dragging?.id === card.id}
                        onDragStart={() => setDragging(card)}
                        onDragEnd={() => {
                          setDragging(null);
                          setOver(null);
                        }}
                        onExtend={() => extend(card)}
                        onCancel={() => setCancelBooking(card)}
                        onToCustomer={() => setLeadToCustomer(card)}
                      />
                    ))}
                  </AnimatePresence>
                )}
                {!q.isLoading && col.cards.length === 0 && <p className="py-8 text-center text-xs text-muted">Пусто</p>}
              </div>
            </div>
          ))}
        </div>
      )}

      <ConfirmDialog
        open={!!backMove}
        onClose={() => setBackMove(null)}
        loading={busy}
        requireReason
        danger={backMove?.to === 'Cancelled'}
        title={backMove?.to === 'Cancelled' ? 'Отменить сделку?' : `Вернуть на этап «${backMove ? dealStageLabel[backMove.to] : ''}»?`}
        text={backMove?.to === 'Cancelled' ? 'Квартира вернётся в продажу. Отменённую сделку нельзя восстановить.' : 'Статус квартиры изменится вместе с этапом. Причина попадёт в журнал.'}
        confirmLabel={backMove?.to === 'Cancelled' ? 'Отменить сделку' : 'Вернуть'}
        onConfirm={(reason) => backMove && move(backMove.card, backMove.to, reason)}
      />

      <ConfirmDialog
        open={!!cancelBooking}
        onClose={() => setCancelBooking(null)}
        loading={busy}
        requireReason
        danger
        title="Отменить бронь?"
        text="Квартира сразу вернётся в продажу."
        confirmLabel="Отменить бронь"
        onConfirm={async (reason) => {
          if (!cancelBooking) return;
          setBusy(true);
          try {
            await crm.cancelBooking(cancelBooking.id, reason);
            toast.success('Бронь отменена');
            setCancelBooking(null);
            refresh();
          } catch (e) {
            toast.error(e);
          } finally {
            setBusy(false);
          }
        }}
      />

      <CustomerFormModal
        open={!!leadToCustomer}
        onClose={() => setLeadToCustomer(null)}
        lead={leadToCustomer ? { id: leadToCustomer.id, name: leadToCustomer.clientName, phone: leadToCustomer.phone } : null}
      />
    </>
  );
}

function Card({
  card,
  dragging,
  onDragStart,
  onDragEnd,
  onExtend,
  onCancel,
  onToCustomer,
}: {
  card: PipelineCard;
  dragging: boolean;
  onDragStart: () => void;
  onDragEnd: () => void;
  onExtend: () => void;
  onCancel: () => void;
  onToCustomer: () => void;
}) {
  const isDeal = card.kind === 'Deal';
  const expiresSoon = card.expiresAt && new Date(card.expiresAt).getTime() - Date.now() < 24 * 3600_000;
  const body = (
    <>
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="truncate font-medium">{card.clientName}</p>
          <p className="flex items-center gap-1 text-xs text-muted tabular">
            <Phone className="size-3" /> {formatPhone(card.phone)}
          </p>
        </div>
        {isDeal ? <GripVertical className="size-4 shrink-0 text-muted/50 opacity-0 transition group-hover:opacity-100" /> : <Badge tone={card.kind === 'Lead' ? 'blue' : 'amber'}>{card.kind === 'Lead' ? 'Заявка' : 'Бронь'}</Badge>}
      </div>
      <div className="mt-3 flex items-center justify-between text-xs">
        <span className="text-muted">{card.apartmentNumber ? `Кв. ${card.apartmentNumber}` : 'Без квартиры'}</span>
        {card.amount !== null && <span className="font-semibold tabular">{money(card.amount, card.currency)}</span>}
      </div>
      {card.expiresAt && (
        <p className={cn('mt-2 flex items-center gap-1 text-xs', expiresSoon ? 'font-medium text-booked' : 'text-muted')}>
          <Clock className="size-3" /> осталось {timeLeft(card.expiresAt)}
        </p>
      )}
      {!card.expiresAt && <p className="mt-2 text-[11px] text-muted">{relative(card.createdAt)}</p>}
    </>
  );

  return (
    <motion.div
      layout
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: dragging ? 0.4 : 1, y: 0, scale: dragging ? 0.97 : 1 }}
      exit={{ opacity: 0, scale: 0.95 }}
      className="group"
    >
      {isDeal ? (
        <Link
          to={`/crm/deals/${card.id}`}
          draggable
          onDragStart={(e) => {
            e.dataTransfer.effectAllowed = 'move';
            onDragStart();
          }}
          onDragEnd={onDragEnd}
          className="block cursor-grab rounded-xl border border-line/80 bg-white p-3.5 shadow-soft transition-all duration-200 hover:-translate-y-0.5 hover:border-ink/20 hover:shadow-lift active:cursor-grabbing"
        >
          {body}
        </Link>
      ) : (
        <div className="rounded-xl border border-line/80 bg-white p-3.5 shadow-soft transition-all duration-200 hover:shadow-lift">
          {body}
          <div className="mt-3 flex gap-1 border-t border-line/60 pt-2.5">
            {card.kind === 'Lead' ? (
              <button onClick={onToCustomer} className="flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-medium text-ink/70 transition hover:bg-paper hover:text-ink">
                <UserPlus className="size-3.5" /> В клиенты
              </button>
            ) : (
              <>
                <button onClick={onExtend} className="flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-medium text-ink/70 transition hover:bg-paper hover:text-ink">
                  <CalendarPlus className="size-3.5" /> +48 ч
                </button>
                <button onClick={onCancel} className="flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-medium text-ink/70 transition hover:bg-danger-soft hover:text-danger">
                  <XCircle className="size-3.5" /> Отменить
                </button>
              </>
            )}
          </div>
        </div>
      )}
    </motion.div>
  );
}
