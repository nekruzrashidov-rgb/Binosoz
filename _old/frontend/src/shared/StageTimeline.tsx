import { motion } from 'motion/react';
import { Check, X } from 'lucide-react';
import type { DealStage } from '@/api/types';
import { cn } from '@/lib/cn';
import { dealStageLabel } from '@/lib/format';

const FLOW: DealStage[] = ['Lead', 'Showing', 'Booking', 'Contract', 'Payment', 'Completed'];

// Этапы сделки линией: пройденные — золотые, текущий пульсирует. В CRM по клику меняется этап.
export function StageTimeline({ stage, onPick, dark }: { stage: DealStage; onPick?: (s: DealStage) => void; dark?: boolean }) {
  const cancelled = stage === 'Cancelled';
  const current = FLOW.indexOf(stage);
  return (
    <div className="scrollbar-thin overflow-x-auto pb-1">
      <ol className="flex min-w-[560px] items-start">
        {FLOW.map((s, i) => {
          const done = !cancelled && i < current;
          const active = !cancelled && i === current;
          return (
            <li key={s} className="relative flex flex-1 flex-col items-center">
              {i > 0 && (
                <span className={cn('absolute right-1/2 top-4 h-0.5 w-full', dark ? 'bg-white/10' : 'bg-line')}>
                  <motion.span
                    className="block h-full bg-gold"
                    initial={{ width: 0 }}
                    animate={{ width: done || active ? '100%' : 0 }}
                    transition={{ duration: 0.5, delay: i * 0.08 }}
                  />
                </span>
              )}
              <button
                type="button"
                disabled={!onPick || active}
                onClick={() => onPick?.(s)}
                className={cn(
                  'relative z-10 flex size-8 items-center justify-center rounded-full border-2 text-xs font-semibold transition-all duration-300',
                  done && 'border-gold bg-gold text-ink',
                  active && 'border-gold bg-ink text-gold ring-4 ring-gold/25',
                  !done && !active && (dark ? 'border-white/20 bg-ink-2 text-white/40' : 'border-line bg-white text-muted'),
                  onPick && !active && 'cursor-pointer hover:scale-110 hover:border-ink',
                )}
                title={onPick && !active ? `Перевести на этап «${dealStageLabel[s]}»` : undefined}
              >
                {done ? <Check className="size-4" /> : i + 1}
                {active && <span className="absolute inset-0 animate-ping rounded-full bg-gold/30" />}
              </button>
              <span className={cn('mt-2 text-center text-xs font-medium', active ? (dark ? 'text-white' : 'text-ink') : dark ? 'text-white/45' : 'text-muted')}>{dealStageLabel[s]}</span>
            </li>
          );
        })}
      </ol>
      {cancelled && (
        <p className="mt-4 inline-flex items-center gap-2 rounded-full bg-danger-soft px-3 py-1 text-sm font-medium text-danger">
          <X className="size-4" /> Сделка отменена
        </p>
      )}
    </div>
  );
}
