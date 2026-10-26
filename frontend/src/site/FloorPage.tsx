import { useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { AnimatePresence, motion } from 'motion/react';
import { useQuery } from '@tanstack/react-query';
import { ArrowRight, ChevronDown, ChevronRight, ChevronUp, Layers3 } from 'lucide-react';
import { catalog } from '@/api/endpoints';
import type { Apartment } from '@/api/types';
import { ButtonLink, EmptyState, ErrorState, Skeleton, StatusBadge } from '@/components/ui';
import { area, money, roomsLabel } from '@/lib/format';
import { cn } from '@/lib/cn';
import { FloorPlan } from './FloorPlan';
import { useFloors, useMainBuilding } from './hooks';

export default function FloorPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const apartments = useQuery({ queryKey: ['apartments', id], queryFn: () => catalog.apartments(id) });
  const main = useMainBuilding();
  const buildingId = apartments.data?.items[0]?.buildingId ?? main.data?.id;
  const floors = useFloors(buildingId);
  const [hovered, setHovered] = useState<Apartment | null>(null);

  const items = useMemo(() => [...(apartments.data?.items ?? [])].sort((a, b) => a.planSlot - b.planSlot), [apartments.data]);
  const floorIndex = floors.data?.findIndex((f) => f.id === id) ?? -1;
  const floor = floorIndex >= 0 ? floors.data![floorIndex] : undefined;
  const up = floorIndex >= 0 ? floors.data![floorIndex + 1] : undefined;
  const down = floorIndex > 0 ? floors.data![floorIndex - 1] : undefined;

  const open = (a: Apartment) => navigate(`/apartments/${a.id}`);

  return (
    <div className="container-x pb-24 pt-28">
      <nav className="flex flex-wrap items-center gap-1.5 text-sm text-muted">
        <Link to="/" className="hover:text-ink">Главная</Link>
        <ChevronRight className="size-3.5" />
        {buildingId && (
          <>
            <Link to={`/building/${buildingId}`} className="hover:text-ink">Выбор на 3D</Link>
            <ChevronRight className="size-3.5" />
          </>
        )}
        <span className="text-ink">Этаж {floor?.number ?? items[0]?.floorNumber ?? ''}</span>
      </nav>

      <div className="mt-6 flex flex-wrap items-end justify-between gap-6">
        <div>
          <p className="eyebrow">План этажа</p>
          <h1 className="display mt-2 text-5xl sm:text-6xl">
            {floor?.isCommercial ? 'Коммерческий этаж' : `${floor?.number ?? items[0]?.floorNumber ?? '…'} этаж`}
          </h1>
        </div>
        <div className="flex items-center gap-2">
          <FloorNav to={down?.id} label="Этаж ниже" icon={<ChevronDown className="size-4" />} />
          <div className="flex h-10 min-w-24 items-center justify-center gap-2 rounded-full border border-line bg-white px-4 text-sm">
            <Layers3 className="size-4 text-gold-2" />
            <span className="tabular">{floor ? `${floor.freeApartments} своб.` : '—'}</span>
          </div>
          <FloorNav to={up?.id} label="Этаж выше" icon={<ChevronUp className="size-4" />} />
        </div>
      </div>

      {apartments.error ? (
        <ErrorState error={apartments.error} onRetry={() => apartments.refetch()} />
      ) : (
        <div className="mt-10 grid gap-8 lg:grid-cols-[1fr_340px]">
          <div className="relative rounded-[28px] border border-line/80 bg-white p-4 shadow-soft sm:p-8">
            <AnimatePresence mode="wait">
              {apartments.isLoading ? (
                <Skeleton className="aspect-[1000/520] w-full" />
              ) : items.length === 0 ? (
                <EmptyState title="На этом этаже нет квартир" text="Выберите другой этаж на модели дома." />
              ) : (
                <motion.div key={id} initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }}>
                  <FloorPlan apartments={items} hoveredId={hovered?.id} onHover={setHovered} onSelect={open} />
                </motion.div>
              )}
            </AnimatePresence>
            <div className="mt-6 flex flex-wrap gap-4 text-xs text-muted">
              {(['Free', 'Booked', 'Sold', 'OnRequest'] as const).map((s) => (
                <StatusBadge key={s} status={s} />
              ))}
            </div>
          </div>

          <aside className="flex flex-col gap-3">
            <AnimatePresence mode="wait">
              {hovered ? (
                <motion.div key={hovered.id} initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }} transition={{ duration: 0.18 }} className="rounded-3xl bg-ink p-6 text-white shadow-lift">
                  <div className="flex items-center justify-between">
                    <p className="eyebrow text-gold">Квартира</p>
                    <StatusBadge status={hovered.status} />
                  </div>
                  <p className="display mt-2 text-6xl">{hovered.number}</p>
                  <p className="mt-2 text-white/60">{roomsLabel(hovered.rooms)} · {area(hovered.area)}</p>
                  <p className="mt-5 text-2xl font-semibold tabular">{hovered.status === 'OnRequest' ? 'Цена по запросу' : money(hovered.price, hovered.currency)}</p>
                  {hovered.status !== 'OnRequest' && <p className="text-sm text-white/45 tabular">{money(hovered.pricePerM2)} за м²</p>}
                </motion.div>
              ) : (
                <motion.div key="hint" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} className="rounded-3xl border border-dashed border-line p-6 text-sm text-muted">
                  Наведите на квартиру на плане, чтобы увидеть цену, или выберите из списка.
                </motion.div>
              )}
            </AnimatePresence>

            <div className="rounded-3xl border border-line/80 bg-white p-2 shadow-soft">
              {apartments.isLoading
                ? Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="m-1 h-14" />)
                : items.map((a) => (
                    <Link
                      key={a.id}
                      to={`/apartments/${a.id}`}
                      onMouseEnter={() => setHovered(a)}
                      onMouseLeave={() => setHovered(null)}
                      className={cn('group flex items-center gap-3 rounded-2xl px-3 py-2.5 transition-colors', hovered?.id === a.id ? 'bg-paper' : 'hover:bg-paper')}
                    >
                      <span className="w-12 font-display text-xl tabular">{a.number}</span>
                      <span className="flex-1">
                        <span className="block text-sm font-medium">{roomsLabel(a.rooms)}</span>
                        <span className="block text-xs text-muted">{area(a.area)}</span>
                      </span>
                      <span className="text-right">
                        <span className={cn('block text-sm font-semibold tabular', a.status === 'Sold' && 'text-muted line-through')}>
                          {a.status === 'OnRequest' ? 'по запросу' : money(a.price, a.currency)}
                        </span>
                        <StatusDot status={a.status} />
                      </span>
                    </Link>
                  ))}
            </div>
            {buildingId && (
              <ButtonLink to={`/building/${buildingId}`} variant="secondary" iconRight={<ArrowRight className="size-4" />}>
                Вернуться к 3D-модели
              </ButtonLink>
            )}
          </aside>
        </div>
      )}
    </div>
  );
}

function StatusDot({ status }: { status: Apartment['status'] }) {
  const map = { Free: 'text-free', Booked: 'text-booked', Sold: 'text-sold', OnRequest: 'text-request' } as const;
  const label = { Free: 'свободна', Booked: 'бронь', Sold: 'продана', OnRequest: 'под запрос' } as const;
  return <span className={cn('block text-[11px] font-medium', map[status])}>{label[status]}</span>;
}

function FloorNav({ to, label, icon }: { to?: string; label: string; icon: React.ReactNode }) {
  if (!to)
    return (
      <span className="flex size-10 items-center justify-center rounded-full border border-line text-muted/40" aria-hidden>
        {icon}
      </span>
    );
  return (
    <Link to={`/floors/${to}`} aria-label={label} title={label} className="flex size-10 items-center justify-center rounded-full border border-line bg-white transition-all hover:-translate-y-0.5 hover:border-ink hover:bg-ink hover:text-white">
      {icon}
    </Link>
  );
}
