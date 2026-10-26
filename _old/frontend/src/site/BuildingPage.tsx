import { lazy, Suspense, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { AnimatePresence, motion } from 'motion/react';
import { useQuery } from '@tanstack/react-query';
import { ArrowRight, ChevronRight, Layers3 } from 'lucide-react';
import { catalog } from '@/api/endpoints';
import { useLang } from '@/api/session';
import type { Floor } from '@/api/types';
import { ButtonLink, ErrorState, ProgressBar, Skeleton } from '@/components/ui';
import { cn } from '@/lib/cn';
import { useFloors } from './hooks';

const Building3D = lazy(() => import('./Building3D'));

export default function BuildingPage() {
  const { id = '' } = useParams();
  const lang = useLang();
  const navigate = useNavigate();
  const building = useQuery({ queryKey: ['building', id, lang], queryFn: () => catalog.building(id) });
  const floors = useFloors(id);
  const [hovered, setHovered] = useState<Floor | null>(null);
  const [selected, setSelected] = useState<Floor | null>(null);

  const current = hovered ?? selected;
  const list = [...(floors.data ?? [])].reverse();

  if (building.error) return <div className="pt-32"><ErrorState error={building.error} onRetry={() => building.refetch()} /></div>;

  return (
    <section className="relative min-h-screen overflow-hidden bg-ink text-white">
      <div className="pointer-events-none absolute -left-32 top-32 size-[520px] rounded-full bg-gold/10 blur-3xl" />
      <div className="container-x relative grid gap-8 pb-12 pt-28 lg:grid-cols-[1fr_380px]">
        <div>
          <nav className="flex items-center gap-1.5 text-sm text-white/45">
            <Link to="/" className="hover:text-white">Главная</Link>
            <ChevronRight className="size-3.5" />
            <span className="text-white/80">Выбор на 3D</span>
          </nav>
          <h1 className="display mt-4 text-5xl sm:text-6xl">{building.data?.name ?? <Skeleton className="h-14 w-80 bg-white/10" />}</h1>
          <p className="mt-3 max-w-xl text-white/55">Вращайте дом мышью или пальцем. Наведите на этаж, чтобы увидеть свободные квартиры, и нажмите, чтобы открыть план.</p>

          <div className="relative mt-4 h-[520px] sm:h-[640px]">
            <Suspense fallback={<Skeleton className="h-full bg-white/5" />}>
              {floors.data && (
                <Building3D
                  floors={floors.data}
                  selectedId={current?.id}
                  onHover={setHovered}
                  onSelect={(f) => setSelected(f)}
                  autoRotate={!selected}
                />
              )}
            </Suspense>
            <Legend />
          </div>
        </div>

        <aside className="lg:pt-28">
          <AnimatePresence mode="wait">
            {current ? (
              <motion.div
                key={current.id}
                initial={{ opacity: 0, x: 16 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: -16 }}
                transition={{ duration: 0.25 }}
                className="rounded-3xl border border-white/10 bg-white/[0.04] p-6 backdrop-blur"
              >
                <p className="eyebrow text-gold">{current.isCommercial ? 'Коммерческий этаж' : 'Этаж'}</p>
                <p className="display mt-1 text-7xl">{current.number}</p>
                <div className="mt-6 flex items-baseline justify-between text-sm">
                  <span className="text-white/55">Свободно</span>
                  <span className="text-lg font-semibold tabular">
                    {current.freeApartments} <span className="text-white/40">/ {current.totalApartments}</span>
                  </span>
                </div>
                <ProgressBar className="mt-3 bg-white/10" tone="green" value={current.totalApartments ? (current.freeApartments / current.totalApartments) * 100 : 0} />
                {current.totalApartments > 0 ? (
                  <ButtonLink to={`/floors/${current.id}`} variant="gold" size="lg" className="mt-7 w-full" iconRight={<ArrowRight className="size-4" />}>
                    Открыть план этажа
                  </ButtonLink>
                ) : (
                  <p className="mt-7 rounded-2xl bg-white/5 px-4 py-3 text-sm text-white/60">На этом этаже нет квартир в продаже.</p>
                )}
              </motion.div>
            ) : (
              <motion.div key="hint" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} className="rounded-3xl border border-dashed border-white/15 p-6 text-white/55">
                <Layers3 className="mb-3 size-6 text-gold" />
                Выберите этаж на модели или в списке ниже.
              </motion.div>
            )}
          </AnimatePresence>

          <div className="scrollbar-thin mt-4 max-h-[420px] overflow-y-auto rounded-3xl border border-white/10 bg-white/[0.02] p-2">
            {floors.isLoading
              ? Array.from({ length: 8 }).map((_, i) => <Skeleton key={i} className="my-1 h-12 bg-white/5" />)
              : list.map((f) => (
                  <button
                    key={f.id}
                    onMouseEnter={() => setHovered(f)}
                    onMouseLeave={() => setHovered(null)}
                    onClick={() => (f.totalApartments > 0 ? navigate(`/floors/${f.id}`) : setSelected(f))}
                    className={cn(
                      'group flex w-full items-center gap-4 rounded-2xl px-4 py-3 text-left transition-colors',
                      current?.id === f.id ? 'bg-white/10' : 'hover:bg-white/[0.06]',
                    )}
                  >
                    <span className="w-8 font-display text-2xl tabular">{f.number}</span>
                    <span className="flex-1 text-sm text-white/60">{f.isCommercial ? 'Коммерция' : `${f.freeApartments} свободно`}</span>
                    <span className="flex gap-0.5">
                      {Array.from({ length: Math.min(f.totalApartments, 8) }).map((_, i) => (
                        <span key={i} className={cn('h-4 w-1.5 rounded-full', i < f.freeApartments ? 'bg-free' : 'bg-white/15')} />
                      ))}
                    </span>
                    <ChevronRight className="size-4 text-white/30 transition-transform group-hover:translate-x-0.5 group-hover:text-gold" />
                  </button>
                ))}
          </div>
        </aside>
      </div>
    </section>
  );
}

function Legend() {
  return (
    <div className="pointer-events-none absolute left-0 top-4 flex flex-col gap-2 text-xs text-white/60">
      <span className="flex items-center gap-2"><span className="size-2.5 rounded-full bg-free" /> Есть свободные</span>
      <span className="flex items-center gap-2"><span className="size-2.5 rounded-full bg-[#8f99a6]" /> Всё продано</span>
      <span className="flex items-center gap-2"><span className="size-2.5 rounded-full bg-gold" /> Выбранный этаж</span>
    </div>
  );
}
