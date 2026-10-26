import { forwardRef, useId, useRef, useState, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { ChevronDown, UploadCloud, X } from 'lucide-react';
import { cn } from '@/lib/cn';
import { fileSize } from '@/lib/format';

const control =
  'w-full rounded-xl border border-line bg-white px-3.5 text-[15px] text-ink placeholder:text-muted/70 ' +
  'transition-[border-color,box-shadow] duration-150 hover:border-ink/25 ' +
  'focus:border-gold focus:outline-none focus:ring-4 focus:ring-gold/15 disabled:bg-paper disabled:text-muted';

export function Field({
  label,
  hint,
  error,
  children,
  className,
  htmlFor,
}: {
  label?: string;
  hint?: string;
  error?: string;
  children: ReactNode;
  className?: string;
  htmlFor?: string;
}) {
  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      {label && (
        <label htmlFor={htmlFor} className="text-[13px] font-medium text-ink/80">
          {label}
        </label>
      )}
      {children}
      <AnimatePresence initial={false}>
        {error ? (
          <motion.p
            key="e"
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            className="text-[13px] text-danger"
          >
            {error}
          </motion.p>
        ) : hint ? (
          <p className="text-[12px] text-muted">{hint}</p>
        ) : null}
      </AnimatePresence>
    </div>
  );
}

// prefix — HTML-атрибут со строковым типом, здесь он заменён иконкой слева.
type InputProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'prefix'> & { label?: string; hint?: string; error?: string; prefix?: ReactNode; suffix?: ReactNode };

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { label, hint, error, prefix, suffix, className, id, ...rest },
  ref,
) {
  const auto = useId();
  const inputId = id ?? auto;
  return (
    <Field label={label} hint={hint} error={error} htmlFor={inputId} className={className}>
      <div className="relative">
        {prefix && <span className="pointer-events-none absolute inset-y-0 left-3.5 flex items-center text-muted">{prefix}</span>}
        <input
          ref={ref}
          id={inputId}
          aria-invalid={!!error}
          className={cn(control, 'h-11', prefix && 'pl-10', suffix && 'pr-12', error && 'border-danger focus:border-danger focus:ring-danger/15')}
          {...rest}
        />
        {suffix && <span className="absolute inset-y-0 right-3.5 flex items-center text-sm text-muted">{suffix}</span>}
      </div>
    </Field>
  );
});

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement> & { label?: string; error?: string; hint?: string }>(
  function Textarea({ label, error, hint, className, id, ...rest }, ref) {
    const auto = useId();
    const inputId = id ?? auto;
    return (
      <Field label={label} error={error} hint={hint} htmlFor={inputId} className={className}>
        <textarea ref={ref} id={inputId} rows={3} className={cn(control, 'resize-none py-2.5', error && 'border-danger')} {...rest} />
      </Field>
    );
  },
);

export function Select({
  label,
  error,
  hint,
  className,
  children,
  id,
  ...rest
}: SelectHTMLAttributes<HTMLSelectElement> & { label?: string; error?: string; hint?: string }) {
  const auto = useId();
  const inputId = id ?? auto;
  return (
    <Field label={label} error={error} hint={hint} htmlFor={inputId} className={className}>
      <div className="relative">
        <select id={inputId} className={cn(control, 'h-11 cursor-pointer appearance-none pr-10', error && 'border-danger')} {...rest}>
          {children}
        </select>
        <ChevronDown className="pointer-events-none absolute right-3.5 top-1/2 size-4 -translate-y-1/2 text-muted" />
      </div>
    </Field>
  );
}

// Сегментированный переключатель с плавающей подложкой.
export function Segmented<T extends string>({
  value,
  onChange,
  options,
  className,
  size = 'md',
}: {
  value: T;
  onChange: (v: T) => void;
  options: { value: T; label: ReactNode }[];
  className?: string;
  size?: 'sm' | 'md';
}) {
  const group = useId();
  return (
    <div className={cn('inline-flex rounded-full border border-line bg-white p-1', className)}>
      {options.map((o) => (
        <button
          key={o.value}
          type="button"
          onClick={() => onChange(o.value)}
          className={cn(
            'relative rounded-full font-medium transition-colors duration-200',
            size === 'sm' ? 'h-7 px-3 text-xs' : 'h-8 px-4 text-sm',
            value === o.value ? 'text-white' : 'text-ink/60 hover:text-ink',
          )}
        >
          {value === o.value && (
            <motion.span layoutId={`seg-${group}`} className="absolute inset-0 rounded-full bg-ink" transition={{ type: 'spring', bounce: 0.18, duration: 0.45 }} />
          )}
          <span className="relative">{o.label}</span>
        </button>
      ))}
    </div>
  );
}

export function FileDrop({
  accept,
  file,
  onFile,
  hint,
}: {
  accept?: string;
  file: File | null;
  onFile: (f: File | null) => void;
  hint?: string;
}) {
  const input = useRef<HTMLInputElement>(null);
  const [over, setOver] = useState(false);
  return (
    <div
      onDragOver={(e) => {
        e.preventDefault();
        setOver(true);
      }}
      onDragLeave={() => setOver(false)}
      onDrop={(e) => {
        e.preventDefault();
        setOver(false);
        const f = e.dataTransfer.files[0];
        if (f) onFile(f);
      }}
      onClick={() => input.current?.click()}
      className={cn(
        'group flex cursor-pointer flex-col items-center justify-center rounded-2xl border-2 border-dashed px-6 py-8 text-center transition-all duration-200',
        over ? 'scale-[1.01] border-gold bg-gold-soft/60' : 'border-line bg-paper/60 hover:border-ink/25 hover:bg-paper',
      )}
    >
      <input
        ref={input}
        type="file"
        accept={accept}
        className="hidden"
        onChange={(e) => onFile(e.target.files?.[0] ?? null)}
      />
      {file ? (
        <div className="flex items-center gap-3 rounded-xl bg-white px-4 py-2.5 shadow-soft">
          <span className="max-w-[240px] truncate text-sm font-medium">{file.name}</span>
          <span className="text-xs text-muted">{fileSize(file.size)}</span>
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              onFile(null);
              if (input.current) input.current.value = '';
            }}
            className="rounded-full p-1 text-muted hover:bg-paper hover:text-ink"
            aria-label="Убрать файл"
          >
            <X className="size-3.5" />
          </button>
        </div>
      ) : (
        <>
          <UploadCloud className="mb-2 size-7 text-muted transition-transform duration-300 group-hover:-translate-y-1" />
          <p className="text-sm font-medium">Перетащите файл или нажмите, чтобы выбрать</p>
          {hint && <p className="mt-1 text-xs text-muted">{hint}</p>}
        </>
      )}
    </div>
  );
}

// Поле телефона с фиксированным +992 и маской XX XXX XX XX.
export function PhoneInput({
  value,
  onChange,
  label = 'Телефон',
  error,
  autoFocus,
  dark,
}: {
  value: string;
  onChange: (digits: string) => void;
  label?: string;
  error?: string;
  autoFocus?: boolean;
  dark?: boolean;
}) {
  const id = useId();
  const masked = value
    .replace(/\D/g, '')
    .slice(0, 9)
    .replace(/^(\d{0,2})(\d{0,3})(\d{0,2})(\d{0,2}).*/, (_, a, b, c, d) => [a, b, c, d].filter(Boolean).join(' '));
  return (
    <Field label={label} error={error} htmlFor={id}>
      <div className="relative">
        <span className={cn('pointer-events-none absolute inset-y-0 left-3.5 flex items-center text-[15px] font-medium', dark ? 'text-white/60' : 'text-ink/60')}>
          +992
        </span>
        <input
          id={id}
          inputMode="tel"
          autoComplete="tel-national"
          autoFocus={autoFocus}
          placeholder="90 123 45 67"
          value={masked}
          onChange={(e) => onChange(e.target.value.replace(/\D/g, '').slice(0, 9))}
          className={cn(
            control,
            'h-11 pl-[3.4rem] tabular tracking-wide',
            dark && 'border-white/15 bg-white/5 text-white placeholder:text-white/30 hover:border-white/30',
            error && 'border-danger',
          )}
        />
      </div>
    </Field>
  );
}
