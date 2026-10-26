import { useMemo } from 'react';
import { Link, useSearchParams } from 'react-router';
import { AnimatePresence, motion } from 'motion/react';
import { ArrowUpRight, LayoutGrid, Rows3, SlidersHorizontal, X } from 'lucide-react';
import type { Apartment } from '@/api/types';
import { EmptyState, ErrorState, Segmented, Select, Skeleton, StatusBadge } from '@/components/ui';
import { area, money, roomsLabel, roomsShort } from '@/lib/format';
import { cn } from '@/lib/cn';
import { useAllApartments, useMainBuilding } from './hooks';

type Sort = 'price' | '-price' | 'area' | '-area' | 'floor';

export default function ApartmentsPage() {
  const { data: building } = useMainBuilding();
  const { data, isLoading, error, refetch } = useAllApartments(building?.id);
  const [params, setParams] = useSearchParams();

  const rooms = params.get('rooms');
  const onlyFree = params.get('all') !== '1';
  const sort = (params.get('sort') as Sort) ?? 'price';
  const view = params.get('view') === 'list' ? 'list' : 'grid';
  const maxPrice = Number(params.get('max') ?? 0);

  const set = (key: string, value: string | null) => {
    const next = new URLSearchParams(params);
    if (value === null || value === '') next.delete(key);
    else next.set(key, value);
    setParams(next, { replace: true });
  };

  const priceCeiling = useMemo(() => {
    const prices = (data ?? []).filter((a) => a.status !== 'OnRequest').map((a) => a.price);
    return prices.length ? Math.ceil(Math.max(...prices) / 50_000) * 50_000 : 0;
  }, [data]);

  const list = useMemo(() => {
    let items = [...(data ?? [])];
    if (onlyFree) items = items.filter((a) => a.status === 'Free');
    if (rooms !== null) items = items.filter((a) => String(a.rooms) === rooms);
    if (maxPrice) items = items.filter((a) => a.price <= maxPrice);
    const by: Record<Sort, (a: Apartment, b: Apartment) => number> = {
      price: (a, b) => a.price - b.price,
      '-price': (a, b) => b.price - a.price,
      area: (a, b) => a.area - b.area,
      '-area': (a, b) => b.area - a.area,
      floor: (a, b) => a.floorNumber - b.floorNumber || a.number - b.number,
    };
    return items.sort(by[sort]);
  }, [data, onlyFree, rooms, maxPrice, sort]);

  const roomOptions = useMemo(() => [...new Set((data ?? []).map((a) => a.rooms))].sort(), [data]);
  const filtersOn = rooms !== null || !onlyFree || !!maxPrice;

  return (
    <div className="container-x pb-24 pt-28">
      <p className="eyebrow">Каталог</p>
      <div className="mt-2 flex flex-wrap items-end justify-between gap-4">
        <h1 className="display text-5xl sm:text-6xl">Квартиры</h1>
        <p className="text-sm text-muted">
          {isLoading ? 'Загрузка…' : <>Найдено <span className="font-semibold text-ink tabular">{list.length}</span></>}
        </p>
      </div>

      <div className="sticky top-[72px] z-20 -mx-4 mt-8 border-y border-line/70 bg-paper/85 px-4 py-3 backdrop-blur-xl sm:mx-0 sm:rounded-2xl sm:border">
        <div className="flex flex-wrap items-center gap-3">
          <SlidersHorizontal className="hidden size-4 text-muted sm:block" />
          <div className="scrollbar-thin flex gap-1.5 overflow-x-auto">
            <Chip active={rooms === null} onClick={() => set('rooms', null)}>Все</Chip>
            {roomOptions.map((r) => (
              <Chip key={r} active={rooms === String(r)} onClick={() => set('rooms', String(r))}>
                {roomsShort(r)}
              </Chip>
            ))}
          </div>
          <Chip active={!onlyFree} onClick={() => set('all', onlyFree ? '1' : null)}>
            Показать проданные
          </Chip>
          {priceCeiling > 0 && (
            <Select className="w-44" value={maxPrice || ''} onChange={(e) => set('max', e.target.value)} aria-label="Цена до">
              <option value="">Любая цена</option>
              {[0.4, 0.55, 0.7, 0.85].map((k) => {
                const v = Math.round((priceCeiling * k) / 50_000) * 50_000;
                return (
                  <option key={k} value={v}>
                    до {money(v)}
                  </option>
                );
              })}
            </Select>
          )}
          <Select className="w-48" value={sort} onChange={(e) => set('sort', e.target.value)} aria-label="Сортировка">
            <option value="price">Сначала дешевле</option>
            <option value="-price">Сначала дороже</option>
            <option value="area">Меньше площадь</option>
            <option value="-area">Больше площадь</option>
            <option value="floor">По этажам</option>
          </Select>
          <AnimatePresence>
            {filtersOn && (
              <motion.button
                initial={{ opacity: 0, scale: 0.9 }}
                animate={{ opacity: 1, scale: 1 }}
                exit={{ opacity: 0, scale: 0.9 }}
                onClick={() => setParams({}, { replace: true })}
                className="flex items-center gap-1 text-sm text-muted hover:text-ink"
              >
                <X className="size-3.5" /> Сбросить
              </motion.button>
            )}
          </AnimatePresence>
          <Segmented
            className="ml-auto"
            size="sm"
            value={view}
            onChange={(v) => set('view', v === 'list' ? 'list' : null)}
            options={[
              { value: 'grid', label: <LayoutGrid className="size-3.5" /> },
              { value: 'list', label: <Rows3 className="size-3.5" /> },
            ]}
          />
        </div>
      </div>

      <div className="mt-8">
        {error ? (
          <ErrorState error={error} onRetry={() => refetch()} />
        ) : isLoading ? (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {Array.from({ length: 8 }).map((_, i) => (
              <Skeleton key={i} className="h-64 rounded-2xl" />
            ))}
          </div>
        ) : list.length === 0 ? (
          <EmptyState title="Ничего не нашлось" text="Измените фильтры или оставьте заявку — подберём квартиру вместе." />
        ) : view === 'grid' ? (
          <motion.div layout className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            <AnimatePresence mode="popLayout">
              {list.map((a, i) => (
                <ApartmentCard key={a.id} a={a} index={i} />
              ))}
            </AnimatePresence>
          </motion.div>
        ) : (
          <div className="overflow-hidden rounded-2xl border border-line/80 bg-white">
            {list.map((a) => (
              <Link
                key={a.id}
                to={`/apartments/${a.id}`}
                className="group grid grid-cols-[64px_1fr_auto] items-center gap-4 border-b border-line/60 px-5 py-4 transition-colors last:border-0 hover:bg-paper sm:grid-cols-[80px_1fr_120px_120px_160px_32px]"
              >
                <span className="font-display text-2xl tabular">{a.number}</span>
                <span className="text-sm font-medium">{roomsLabel(a.rooms)}</span>
                <span className="hidden text-sm text-muted sm:block">{area(a.area)}</span>
                <span className="hidden text-sm text-muted sm:block">{a.floorNumber} этаж</span>
                <span className="text-right text-sm font-semibold tabular">{a.status === 'OnRequest' ? 'по запросу' : money(a.price, a.currency)}</span>
                <ArrowUpRight className="hidden size-4 text-muted transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5 group-hover:text-ink sm:block" />
              </Link>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function Chip({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      className={cn(
        'h-9 shrink-0 rounded-full border px-4 text-sm font-medium transition-all duration-200',
        active ? 'border-ink bg-ink text-white' : 'border-line bg-white text-ink/70 hover:border-ink/30 hover:text-ink',
      )}
    >
      {children}
    </button>
  );
}

function ApartmentCard({ a, index }: { a: Apartment; index: number }) {
  return (
    <motion.div
      layout
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, scale: 0.96 }}
      transition={{ duration: 0.35, delay: Math.min(index, 12) * 0.03 }}
    >
      <Link
        to={`/apartments/${a.id}`}
        className="group relative flex h-full flex-col overflow-hidden rounded-2xl border border-line/80 bg-white p-5 transition-all duration-300 hover:-translate-y-1 hover:border-ink/20 hover:shadow-lift"
      >
        <div className="flex items-start justify-between">
          <StatusBadge status={a.status} />
          <span className="text-xs text-muted">{a.floorNumber} этаж</span>
        </div>
        <div className="my-6 flex items-end gap-3">
          <span className="font-display text-5xl leading-none transition-colors duration-300 group-hover:text-gold-2">{a.number}</span>
          <span className="pb-1 text-sm text-muted">{roomsShort(a.rooms)} · {area(a.area)}</span>
        </div>
        <div className="mt-auto flex items-end justify-between border-t border-line/70 pt-4">
          <div>
            <p className={cn('text-lg font-semibold tabular', a.status === 'Sold' && 'text-muted line-through')}>
              {a.status === 'OnRequest' ? 'По запросу' : money(a.price, a.currency)}
            </p>
            {a.status !== 'OnRequest' && <p className="text-xs text-muted tabular">{money(a.pricePerM2)} / м²</p>}
          </div>
          <span className="flex size-9 items-center justify-center rounded-full bg-paper transition-all duration-300 group-hover:rotate-45 group-hover:bg-ink group-hover:text-gold">
            <ArrowUpRight className="size-4" />
          </span>
        </div>
      </Link>
    </motion.div>
  );
}
