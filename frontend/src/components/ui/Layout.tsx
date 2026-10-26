import { useId, type ReactNode } from 'react';
import { motion } from 'motion/react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { cn } from '@/lib/cn';

export function Card({ className, children, hover }: { className?: string; children: ReactNode; hover?: boolean }) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-line/80 bg-white shadow-soft',
        hover && 'transition-[transform,box-shadow,border-color] duration-300 hover:-translate-y-0.5 hover:border-ink/15 hover:shadow-lift',
        className,
      )}
    >
      {children}
    </div>
  );
}

export function CardHeader({ title, subtitle, action, className }: { title: ReactNode; subtitle?: ReactNode; action?: ReactNode; className?: string }) {
  return (
    <div className={cn('flex flex-wrap items-center justify-between gap-3 border-b border-line/70 px-5 py-4', className)}>
      <div>
        <h3 className="font-semibold">{title}</h3>
        {subtitle && <p className="mt-0.5 text-[13px] text-muted">{subtitle}</p>}
      </div>
      {action}
    </div>
  );
}

export function StatCard({
  label,
  value,
  hint,
  icon,
  tone = 'default',
  delay = 0,
}: {
  label: string;
  value: ReactNode;
  hint?: ReactNode;
  icon?: ReactNode;
  tone?: 'default' | 'dark' | 'gold';
  delay?: number;
}) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay, duration: 0.45, ease: [0.22, 1, 0.36, 1] }}
      className={cn(
        'group relative overflow-hidden rounded-2xl border p-5 transition-[transform,box-shadow] duration-300 hover:-translate-y-0.5 hover:shadow-lift',
        tone === 'dark' && 'border-ink bg-ink text-white',
        tone === 'gold' && 'border-gold/40 bg-gold-soft',
        tone === 'default' && 'border-line/80 bg-white shadow-soft',
      )}
    >
      <div className="flex items-start justify-between">
        <p className={cn('text-[13px] font-medium', tone === 'dark' ? 'text-white/60' : 'text-muted')}>{label}</p>
        {icon && (
          <span
            className={cn(
              'flex size-9 items-center justify-center rounded-xl transition-transform duration-300 group-hover:scale-110 group-hover:rotate-[-4deg]',
              tone === 'dark' ? 'bg-white/10 text-gold' : 'bg-paper text-ink/70',
            )}
          >
            {icon}
          </span>
        )}
      </div>
      <p className="mt-3 text-[28px] font-semibold leading-none tracking-tight tabular">{value}</p>
      {hint && <p className={cn('mt-2 text-[13px]', tone === 'dark' ? 'text-white/55' : 'text-muted')}>{hint}</p>}
    </motion.div>
  );
}

export function Tabs<T extends string>({
  value,
  onChange,
  tabs,
  className,
}: {
  value: T;
  onChange: (v: T) => void;
  tabs: { value: T; label: ReactNode; count?: number }[];
  className?: string;
}) {
  const id = useId();
  return (
    <div className={cn('scrollbar-thin flex gap-1 overflow-x-auto border-b border-line', className)}>
      {tabs.map((t) => (
        <button
          key={t.value}
          onClick={() => onChange(t.value)}
          className={cn(
            'relative flex shrink-0 items-center gap-2 px-3.5 pb-3 pt-2 text-sm font-medium transition-colors',
            value === t.value ? 'text-ink' : 'text-muted hover:text-ink',
          )}
        >
          {t.label}
          {t.count !== undefined && (
            <span className={cn('rounded-full px-1.5 text-[11px] tabular', value === t.value ? 'bg-ink text-white' : 'bg-paper-2 text-muted')}>
              {t.count}
            </span>
          )}
          {value === t.value && (
            <motion.span layoutId={`tab-${id}`} className="absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-ink" transition={{ type: 'spring', bounce: 0.2, duration: 0.4 }} />
          )}
        </button>
      ))}
    </div>
  );
}

export function Pagination({ page, pageSize, total, onChange }: { page: number; pageSize: number; total: number; onChange: (p: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  if (pages <= 1) return null;
  const from = (page - 1) * pageSize + 1;
  const to = Math.min(total, page * pageSize);
  return (
    <div className="flex items-center justify-between gap-3 px-5 py-3 text-sm text-muted">
      <span className="tabular">
        {from}–{to} из {total}
      </span>
      <div className="flex items-center gap-1">
        <button
          disabled={page <= 1}
          onClick={() => onChange(page - 1)}
          className="flex size-8 items-center justify-center rounded-full transition hover:bg-paper hover:text-ink disabled:opacity-30"
          aria-label="Назад"
        >
          <ChevronLeft className="size-4" />
        </button>
        <span className="min-w-14 text-center tabular text-ink">
          {page} / {pages}
        </span>
        <button
          disabled={page >= pages}
          onClick={() => onChange(page + 1)}
          className="flex size-8 items-center justify-center rounded-full transition hover:bg-paper hover:text-ink disabled:opacity-30"
          aria-label="Вперёд"
        >
          <ChevronRight className="size-4" />
        </button>
      </div>
    </div>
  );
}

// Таблица с плавным появлением строк и подсветкой при наведении.
export function Table({ head, children, className }: { head: ReactNode[]; children: ReactNode; className?: string }) {
  return (
    <div className={cn('scrollbar-thin overflow-x-auto', className)}>
      <table className="w-full min-w-[640px] text-left text-sm">
        <thead>
          <tr className="border-b border-line text-[12px] uppercase tracking-wider text-muted">
            {head.map((h, i) => (
              <th key={i} className="whitespace-nowrap px-5 py-3 font-medium">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="[&>tr]:border-b [&>tr]:border-line/60 [&>tr:last-child]:border-0 [&>tr]:transition-colors [&>tr:hover]:bg-paper/70 [&_td]:px-5 [&_td]:py-3.5">
          {children}
        </tbody>
      </table>
    </div>
  );
}

export function PageHeader({ title, subtitle, actions, eyebrow }: { title: ReactNode; subtitle?: ReactNode; actions?: ReactNode; eyebrow?: ReactNode }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: -6 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.35 }}
      className="mb-6 flex flex-wrap items-end justify-between gap-4"
    >
      <div>
        {eyebrow && <div className="eyebrow mb-1.5">{eyebrow}</div>}
        <h1 className="text-[26px] font-semibold tracking-tight">{title}</h1>
        {subtitle && <p className="mt-1 text-sm text-muted">{subtitle}</p>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </motion.div>
  );
}

// Появление блока при прокрутке.
export function Reveal({ children, delay = 0, className, y = 24 }: { children: ReactNode; delay?: number; className?: string; y?: number }) {
  return (
    <motion.div
      className={className}
      initial={{ opacity: 0, y }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: '-60px' }}
      transition={{ duration: 0.7, delay, ease: [0.22, 1, 0.36, 1] }}
    >
      {children}
    </motion.div>
  );
}

export function KeyValue({ label, value, className }: { label: ReactNode; value: ReactNode; className?: string }) {
  return (
    <div className={cn('flex items-baseline justify-between gap-4 py-2.5', className)}>
      <span className="text-sm text-muted">{label}</span>
      <span className="text-right text-sm font-medium tabular">{value}</span>
    </div>
  );
}
