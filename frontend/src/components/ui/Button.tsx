import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react';
import { Link, type LinkProps } from 'react-router';
import { cn } from '@/lib/cn';
import { Spinner } from './Feedback';

type Variant = 'primary' | 'gold' | 'secondary' | 'ghost' | 'danger' | 'outline-light';
type Size = 'sm' | 'md' | 'lg';

const base =
  'group/btn relative inline-flex select-none items-center justify-center gap-2 whitespace-nowrap rounded-full font-medium ' +
  'transition-[background-color,color,box-shadow,transform,border-color] duration-200 ease-out ' +
  'active:scale-[0.97] disabled:pointer-events-none disabled:opacity-50';

const variants: Record<Variant, string> = {
  primary: 'bg-ink text-white shadow-[0_8px_20px_-10px_rgb(15_17_21/0.6)] hover:bg-ink-3 hover:shadow-[0_14px_28px_-12px_rgb(15_17_21/0.55)]',
  gold: 'bg-gold text-ink shadow-[0_10px_24px_-12px_rgb(176_138_79/0.9)] hover:bg-[#d4b27b] hover:shadow-[0_16px_32px_-12px_rgb(176_138_79/0.8)]',
  secondary: 'border border-line bg-white text-ink hover:border-ink/25 hover:bg-paper',
  ghost: 'text-ink hover:bg-ink/[0.05]',
  danger: 'bg-danger text-white hover:bg-[#bd3c33]',
  'outline-light': 'border border-white/30 text-white backdrop-blur hover:border-white/70 hover:bg-white/10',
};

const sizes: Record<Size, string> = {
  sm: 'h-8 px-3.5 text-[13px]',
  md: 'h-10 px-5 text-sm',
  lg: 'h-12 px-7 text-[15px]',
};

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
  loading?: boolean;
  icon?: ReactNode;
  iconRight?: ReactNode;
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = 'primary', size = 'md', loading, icon, iconRight, className, children, disabled, type = 'button', ...rest },
  ref,
) {
  return (
    <button
      ref={ref}
      type={type}
      disabled={disabled || loading}
      className={cn(base, variants[variant], sizes[size], className)}
      {...rest}
    >
      {loading ? <Spinner className="size-4" /> : icon}
      {children}
      {iconRight && (
        <span className="transition-transform duration-200 group-hover/btn:translate-x-0.5">{iconRight}</span>
      )}
    </button>
  );
});

export function ButtonLink({
  variant = 'primary',
  size = 'md',
  className,
  children,
  icon,
  iconRight,
  ...rest
}: LinkProps & { variant?: Variant; size?: Size; icon?: ReactNode; iconRight?: ReactNode }) {
  return (
    <Link className={cn(base, variants[variant], sizes[size], className)} {...rest}>
      {icon}
      {children}
      {iconRight && (
        <span className="transition-transform duration-200 group-hover/btn:translate-x-0.5">{iconRight}</span>
      )}
    </Link>
  );
}

export function IconButton({
  label,
  className,
  children,
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & { label: string }) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      className={cn(
        'inline-flex size-9 items-center justify-center rounded-full text-ink/70 transition-colors duration-150 hover:bg-ink/[0.06] hover:text-ink active:scale-95 disabled:opacity-40',
        className,
      )}
      {...rest}
    >
      {children}
    </button>
  );
}
