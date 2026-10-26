import { cn } from '@/lib/cn';

export function Logo({ light, compact }: { light?: boolean; compact?: boolean }) {
  return (
    <span className={cn('group inline-flex items-center gap-2.5', light ? 'text-white' : 'text-ink')}>
      <svg viewBox="0 0 32 32" className="size-8 transition-transform duration-500 group-hover:rotate-[72deg]" aria-hidden>
        <path d="M16 3l3 9.2h9.7l-7.8 5.7 3 9.2L16 21.4l-7.9 5.7 3-9.2-7.8-5.7H13z" fill="var(--color-gold)" />
      </svg>
      {!compact && (
        <span className="flex flex-col leading-none">
          <span className="font-display text-[22px] tracking-wide">Sitora</span>
          <span className={cn('mt-0.5 text-[9px] font-semibold uppercase tracking-[0.34em]', light ? 'text-white/50' : 'text-muted')}>Residence</span>
        </span>
      )}
    </span>
  );
}
