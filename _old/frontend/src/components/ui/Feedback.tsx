import type { ReactNode } from 'react';
import { motion } from 'motion/react';
import { AlertTriangle, Inbox, RotateCw } from 'lucide-react';
import { cn } from '@/lib/cn';
import { ApiError } from '@/api/client';
import type { ApartmentStatus } from '@/api/types';
import { apartmentStatusLabel } from '@/lib/format';

export function Spinner({ className }: { className?: string }) {
  return (
    <svg className={cn('animate-spin', className ?? 'size-5')} viewBox="0 0 24 24" fill="none" aria-hidden>
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeOpacity="0.2" strokeWidth="3" />
      <path d="M21 12a9 9 0 0 0-9-9" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
    </svg>
  );
}

export function Skeleton({ className }: { className?: string }) {
  return <div className={cn('skeleton', className)} />;
}

export function EmptyState({
  icon,
  title,
  text,
  action,
  className,
}: {
  icon?: ReactNode;
  title: string;
  text?: string;
  action?: ReactNode;
  className?: string;
}) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      className={cn('flex flex-col items-center justify-center px-6 py-14 text-center', className)}
    >
      <div className="mb-4 flex size-14 items-center justify-center rounded-2xl bg-paper-2 text-muted">
        {icon ?? <Inbox className="size-6" />}
      </div>
      <p className="font-medium">{title}</p>
      {text && <p className="mt-1 max-w-sm text-sm text-muted">{text}</p>}
      {action && <div className="mt-5">{action}</div>}
    </motion.div>
  );
}

export function ErrorState({ error, onRetry, className }: { error: unknown; onRetry?: () => void; className?: string }) {
  const message = error instanceof ApiError ? error.message : 'Не удалось загрузить данные.';
  return (
    <div className={cn('flex flex-col items-center px-6 py-12 text-center', className)}>
      <div className="mb-4 flex size-12 items-center justify-center rounded-2xl bg-danger-soft text-danger">
        <AlertTriangle className="size-5" />
      </div>
      <p className="font-medium">{message}</p>
      {onRetry && (
        <button
          onClick={onRetry}
          className="mt-4 inline-flex items-center gap-1.5 text-sm font-medium text-gold-2 hover:text-ink"
        >
          <RotateCw className="size-3.5" /> Повторить
        </button>
      )}
    </div>
  );
}

const statusStyles: Record<ApartmentStatus, string> = {
  Free: 'bg-free-soft text-free',
  Booked: 'bg-booked-soft text-[#a8721a]',
  Sold: 'bg-sold-soft text-[#6b717b]',
  OnRequest: 'bg-request-soft text-request',
};

const dotStyles: Record<ApartmentStatus, string> = {
  Free: 'bg-free',
  Booked: 'bg-booked',
  Sold: 'bg-sold',
  OnRequest: 'bg-request',
};

export function StatusBadge({ status, className }: { status: ApartmentStatus; className?: string }) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium',
        statusStyles[status],
        className,
      )}
    >
      <span className={cn('size-1.5 rounded-full', dotStyles[status], status === 'Free' && 'animate-pulse')} />
      {apartmentStatusLabel[status]}
    </span>
  );
}

type Tone = 'neutral' | 'gold' | 'green' | 'amber' | 'red' | 'blue' | 'dark';
const tones: Record<Tone, string> = {
  neutral: 'bg-paper-2 text-ink/70',
  gold: 'bg-gold-soft text-gold-2',
  green: 'bg-free-soft text-free',
  amber: 'bg-booked-soft text-[#a8721a]',
  red: 'bg-danger-soft text-danger',
  blue: 'bg-request-soft text-request',
  dark: 'bg-ink text-white',
};

export function Badge({ tone = 'neutral', children, className }: { tone?: Tone; children: ReactNode; className?: string }) {
  return (
    <span className={cn('inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-medium', tones[tone], className)}>
      {children}
    </span>
  );
}

export function ProgressBar({ value, className, tone = 'gold' }: { value: number; className?: string; tone?: 'gold' | 'green' | 'dark' }) {
  const color = tone === 'green' ? 'bg-free' : tone === 'dark' ? 'bg-ink' : 'bg-gold';
  return (
    <div className={cn('h-1.5 overflow-hidden rounded-full bg-ink/[0.07]', className)}>
      <motion.div
        className={cn('h-full rounded-full', color)}
        initial={{ width: 0 }}
        whileInView={{ width: `${Math.min(100, Math.max(0, value))}%` }}
        viewport={{ once: true }}
        transition={{ duration: 1.1, ease: [0.22, 1, 0.36, 1] }}
      />
    </div>
  );
}

export function ProgressRing({ value, size = 120, stroke = 8, label }: { value: number; size?: number; stroke?: number; label?: string }) {
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  return (
    <div className="relative inline-flex items-center justify-center" style={{ width: size, height: size }}>
      <svg width={size} height={size} className="-rotate-90">
        <circle cx={size / 2} cy={size / 2} r={r} stroke="currentColor" strokeOpacity={0.1} strokeWidth={stroke} fill="none" />
        <motion.circle
          cx={size / 2}
          cy={size / 2}
          r={r}
          stroke="var(--color-gold)"
          strokeWidth={stroke}
          strokeLinecap="round"
          fill="none"
          strokeDasharray={c}
          initial={{ strokeDashoffset: c }}
          whileInView={{ strokeDashoffset: c * (1 - value / 100) }}
          viewport={{ once: true }}
          transition={{ duration: 1.4, ease: [0.22, 1, 0.36, 1] }}
        />
      </svg>
      <div className="absolute flex flex-col items-center">
        <span className="display text-3xl tabular">{value}%</span>
        {label && <span className="text-[11px] uppercase tracking-widest opacity-60">{label}</span>}
      </div>
    </div>
  );
}
