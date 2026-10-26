import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { motion } from 'motion/react';
import { CalendarClock, ExternalLink, Handshake } from 'lucide-react';
import type { Apartment, ApartmentStatus } from '@/api/types';
import { Button, Drawer, ErrorState, KeyValue, PageHeader, Skeleton, StatusBadge } from '@/components/ui';
import { cn } from '@/lib/cn';
import { apartmentStatusLabel, area, money, roomsLabel, roomsShort } from '@/lib/format';
import { useAllApartments, useMainBuilding } from '@/site/hooks';
import { ROLES } from '../access';
import { BookForCustomerModal, DealFormModal } from '../forms';
import { Guard } from '../Guard';

const CELL: Record<ApartmentStatus, string> = {
  Free: 'bg-free-soft border-free/30 text-free hover:bg-free hover:text-white',
  Booked: 'bg-booked-soft border-booked/30 text-[#a8721a] hover:bg-booked hover:text-white',
  Sold: 'bg-sold-soft border-transparent text-[#8b919b] hover:bg-sold hover:text-white',
  OnRequest: 'bg-request-soft border-request/30 text-request hover:bg-request hover:text-white',
};

export default function Chessboard() {
  return (
    <Guard roles={ROLES.sales}>
      <Content />
    </Guard>
  );
}

function Content() {
  const navigate = useNavigate();
  const { data: building } = useMainBuilding();
  const { data, floors, isLoading, error, refetch } = useAllApartments(building?.id);
  const [filter, setFilter] = useState<ApartmentStatus | 'all'>('all');
  const [selected, setSelected] = useState<Apartment | null>(null);
  const [book, setBook] = useState<Apartment | null>(null);
  const [deal, setDeal] = useState<Apartment | null>(null);

  const rows = useMemo(() => {
    const byFloor = new Map<string, Apartment[]>();
    for (const a of data ?? []) byFloor.set(a.floorId, [...(byFloor.get(a.floorId) ?? []), a]);
    return [...(floors ?? [])]
      .sort((a, b) => b.number - a.number)
      .map((f) => ({ floor: f, items: (byFloor.get(f.id) ?? []).sort((a, b) => a.planSlot - b.planSlot) }))
      .filter((r) => r.items.length > 0);
  }, [data, floors]);

  const counts = useMemo(() => {
    const c: Record<string, number> = {};
    for (const a of data ?? []) c[a.status] = (c[a.status] ?? 0) + 1;
    return c;
  }, [data]);

  return (
    <>
      <PageHeader title="Шахматка" subtitle={building ? `${building.name} · ${data?.length ?? 0} квартир` : undefined} />

      <div className="mb-5 flex flex-wrap gap-2">
        {(['all', 'Free', 'Booked', 'Sold', 'OnRequest'] as const).map((s) => (
          <button
            key={s}
            onClick={() => setFilter(s)}
            className={cn(
              'flex h-9 items-center gap-2 rounded-full border px-4 text-sm font-medium transition-all',
              filter === s ? 'border-ink bg-ink text-white' : 'border-line bg-white text-ink/70 hover:border-ink/30',
            )}
          >
            {s === 'all' ? 'Все' : apartmentStatusLabel[s]}
            <span className={cn('rounded-full px-1.5 text-xs tabular', filter === s ? 'bg-white/15' : 'bg-paper-2')}>
              {s === 'all' ? data?.length ?? 0 : counts[s] ?? 0}
            </span>
          </button>
        ))}
      </div>

      {error ? (
        <ErrorState error={error} onRetry={() => refetch()} />
      ) : isLoading ? (
        <div className="flex flex-col gap-2">{Array.from({ length: 10 }).map((_, i) => <Skeleton key={i} className="h-14" />)}</div>
      ) : (
        <div className="scrollbar-thin overflow-x-auto rounded-2xl border border-line/80 bg-white p-4 shadow-soft">
          <div className="flex min-w-[760px] flex-col gap-1.5">
            {rows.map(({ floor, items }, ri) => (
              <motion.div key={floor.id} initial={{ opacity: 0, x: -8 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: ri * 0.02 }} className="flex items-stretch gap-1.5">
                <div className="flex w-14 shrink-0 flex-col items-center justify-center rounded-lg bg-paper text-center">
                  <span className="font-display text-lg leading-none">{floor.number}</span>
                  <span className="text-[10px] text-muted">{floor.freeApartments} св.</span>
                </div>
                {items.map((a) => {
                  const dim = filter !== 'all' && a.status !== filter;
                  return (
                    <button
                      key={a.id}
                      onClick={() => setSelected(a)}
                      className={cn(
                        'group relative flex h-14 flex-1 flex-col items-start justify-center rounded-lg border px-2.5 text-left transition-all duration-200 hover:z-10 hover:scale-[1.06] hover:shadow-lift',
                        CELL[a.status],
                        dim && 'opacity-20',
                        selected?.id === a.id && 'ring-2 ring-ink ring-offset-1',
                      )}
                    >
                      <span className="flex w-full items-baseline justify-between">
                        <span className="text-sm font-semibold tabular">{a.number}</span>
                        <span className="text-[10px] opacity-75">{roomsShort(a.rooms)}</span>
                      </span>
                      <span className="text-[11px] tabular opacity-80">{a.status === 'OnRequest' ? 'запрос' : `${Math.round(a.price / 1000)} т.`}</span>
                    </button>
                  );
                })}
              </motion.div>
            ))}
          </div>
        </div>
      )}

      <Drawer open={!!selected} onClose={() => setSelected(null)} title={selected ? `Квартира №${selected.number}` : ''} width={440}>
        {selected && (
          <div className="flex flex-col gap-6">
            <div className="rounded-2xl bg-ink p-6 text-white">
              <StatusBadge status={selected.status} />
              <p className="display mt-4 text-5xl">{selected.number}</p>
              <p className="mt-1 text-white/55">{roomsLabel(selected.rooms)} · {area(selected.area)} · {selected.floorNumber} этаж</p>
              <p className="mt-5 text-3xl font-semibold tabular">{money(selected.price, selected.currency)}</p>
              <p className="text-sm text-white/45 tabular">{money(selected.pricePerM2)} за м²</p>
            </div>
            <div className="divide-y divide-line/70">
              <KeyValue label="Позиция на плане" value={selected.planSlot + 1} />
              <KeyValue label="Объект в 3D" value={selected.model3dId ?? '—'} />
            </div>
            <div className="flex flex-col gap-2">
              {selected.status === 'Free' && (
                <Button size="lg" icon={<CalendarClock className="size-4" />} onClick={() => setBook(selected)}>
                  Забронировать на клиента
                </Button>
              )}
              {selected.status !== 'Sold' && (
                <Button size="lg" variant="secondary" icon={<Handshake className="size-4" />} onClick={() => setDeal(selected)}>
                  Создать сделку
                </Button>
              )}
              <Link to={`/apartments/${selected.id}`} target="_blank" className="mt-2 inline-flex items-center justify-center gap-1.5 text-sm text-muted hover:text-ink">
                Открыть на сайте <ExternalLink className="size-3.5" />
              </Link>
            </div>
          </div>
        )}
      </Drawer>

      <BookForCustomerModal open={!!book} onClose={() => { setBook(null); setSelected(null); }} apartment={book} />
      <DealFormModal open={!!deal} onClose={() => setDeal(null)} apartment={deal} onCreated={(d) => navigate(`/crm/deals/${d.id}`)} />
    </>
  );
}
