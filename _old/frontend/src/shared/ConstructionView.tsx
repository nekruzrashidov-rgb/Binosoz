import { useState } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { Camera, ChevronLeft, ChevronRight, HardHat, X } from 'lucide-react';
import { createPortal } from 'react-dom';
import type { BuildingProgress, Photo } from '@/api/types';
import { Card, EmptyState, ProgressBar, ProgressRing } from '@/components/ui';
import { cn } from '@/lib/cn';
import { dateShort } from '@/lib/format';

// Ход стройки: общий процент, этапы и фото (ссылки на фото подписаны на 15 минут).
export function ConstructionView({ progress, actions }: { progress: BuildingProgress; actions?: (stageId: string) => React.ReactNode }) {
  const [viewer, setViewer] = useState<{ photos: Photo[]; index: number } | null>(null);
  const stages = [...progress.stages].sort((a, b) => a.sortOrder - b.sortOrder);

  if (stages.length === 0) return <EmptyState title="Этапы стройки ещё не добавлены" icon={<HardHat className="size-6" />} />;

  return (
    <div className="grid gap-6 lg:grid-cols-[280px_1fr]">
      <Card className="flex flex-col items-center justify-center bg-ink p-8 text-white lg:sticky lg:top-6 lg:self-start">
        <ProgressRing value={progress.overallPercent} size={180} stroke={10} label="готовность" />
        <p className="mt-6 text-center text-sm text-white/55">Среднее по {stages.length} этапам</p>
      </Card>

      <div className="relative flex flex-col gap-4">
        <div className="absolute bottom-6 left-[19px] top-6 w-px bg-line" />
        {stages.map((s, i) => {
          const done = s.percent >= 100;
          return (
            <motion.div key={s.id} initial={{ opacity: 0, x: 12 }} whileInView={{ opacity: 1, x: 0 }} viewport={{ once: true }} transition={{ delay: i * 0.05 }} className="relative flex gap-4">
              <span
                className={cn(
                  'relative z-10 mt-5 flex size-10 shrink-0 items-center justify-center rounded-full border-4 border-paper text-xs font-semibold',
                  done ? 'bg-free text-white' : s.percent > 0 ? 'bg-gold text-ink' : 'bg-paper-2 text-muted',
                )}
              >
                {i + 1}
              </span>
              <Card className="flex-1 p-5" hover>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <p className="font-semibold">{s.name}</p>
                    <p className="text-xs text-muted">Обновлено {dateShort(s.updatedAt)}</p>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="display text-3xl tabular">{s.percent}%</span>
                    {actions?.(s.id)}
                  </div>
                </div>
                <ProgressBar value={s.percent} tone={done ? 'green' : 'gold'} className="mt-4" />
                {s.photos.length > 0 && (
                  <div className="mt-4 flex gap-2 overflow-x-auto pb-1">
                    {s.photos.map((p, pi) => (
                      <button
                        key={p.id}
                        onClick={() => setViewer({ photos: s.photos, index: pi })}
                        className="group relative size-24 shrink-0 overflow-hidden rounded-xl bg-paper-2"
                      >
                        <img src={p.url} alt={p.caption ?? s.name} loading="lazy" className="size-full object-cover transition-transform duration-500 group-hover:scale-110" />
                        <span className="absolute inset-0 flex items-center justify-center bg-ink/0 text-white opacity-0 transition group-hover:bg-ink/35 group-hover:opacity-100">
                          <Camera className="size-5" />
                        </span>
                      </button>
                    ))}
                  </div>
                )}
              </Card>
            </motion.div>
          );
        })}
      </div>

      <PhotoViewer state={viewer} onChange={setViewer} />
    </div>
  );
}

function PhotoViewer({ state, onChange }: { state: { photos: Photo[]; index: number } | null; onChange: (s: { photos: Photo[]; index: number } | null) => void }) {
  const photo = state?.photos[state.index];
  const go = (delta: number) => state && onChange({ ...state, index: (state.index + delta + state.photos.length) % state.photos.length });
  return createPortal(
    <AnimatePresence>
      {state && photo && (
        <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} className="fixed inset-0 z-[90] flex items-center justify-center bg-ink/90 p-4 backdrop-blur" onClick={() => onChange(null)}>
          <motion.img
            key={photo.id}
            src={photo.url}
            alt={photo.caption ?? ''}
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="max-h-[82vh] max-w-full rounded-2xl object-contain shadow-lift"
            onClick={(e) => e.stopPropagation()}
          />
          <div className="absolute bottom-6 left-1/2 -translate-x-1/2 text-center text-sm text-white/80">
            {photo.caption} <span className="text-white/45">· {dateShort(photo.takenAt)}</span>
          </div>
          {state.photos.length > 1 && (
            <>
              <button onClick={(e) => { e.stopPropagation(); go(-1); }} className="absolute left-4 flex size-12 items-center justify-center rounded-full bg-white/10 text-white hover:bg-white/20" aria-label="Предыдущее фото">
                <ChevronLeft />
              </button>
              <button onClick={(e) => { e.stopPropagation(); go(1); }} className="absolute right-4 flex size-12 items-center justify-center rounded-full bg-white/10 text-white hover:bg-white/20" aria-label="Следующее фото">
                <ChevronRight />
              </button>
            </>
          )}
          <button className="absolute right-4 top-4 flex size-11 items-center justify-center rounded-full bg-white/10 text-white hover:bg-white/20" aria-label="Закрыть">
            <X />
          </button>
        </motion.div>
      )}
    </AnimatePresence>,
    document.body,
  );
}
