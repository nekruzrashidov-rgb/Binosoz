import { useEffect, useRef, useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router';
import { AnimatePresence, motion } from 'motion/react';
import { ArrowLeft, ArrowRight, MessageSquareText, ShieldCheck } from 'lucide-react';
import { ApiError } from '@/api/client';
import { auth } from '@/api/endpoints';
import { setSession, useSession } from '@/api/session';
import { Button, PhoneInput } from '@/components/ui';
import { cn } from '@/lib/cn';
import { formatPhone, normalizePhone } from '@/lib/format';
import { Logo } from '@/site/Logo';

export default function CabinetLogin() {
  const session = useSession('customer');
  const navigate = useNavigate();
  const [step, setStep] = useState<'phone' | 'code'>('phone');
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [resendIn, setResendIn] = useState(0);

  useEffect(() => {
    if (resendIn <= 0) return;
    const t = setTimeout(() => setResendIn((v) => v - 1), 1000);
    return () => clearTimeout(t);
  }, [resendIn]);

  if (session) return <Navigate to="/cabinet" replace />;

  const send = async () => {
    setError(null);
    if (phone.length < 9) return setError('Введите номер телефона полностью');
    setLoading(true);
    try {
      await auth.smsSend(normalizePhone(phone));
      setStep('code');
      setCode('');
      setResendIn(60);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Не удалось отправить код');
    } finally {
      setLoading(false);
    }
  };

  const verify = async (value: string) => {
    setError(null);
    setLoading(true);
    try {
      const response = await auth.smsVerify(normalizePhone(phone), value);
      setSession('customer', response);
      navigate('/cabinet', { replace: true });
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Неверный код');
      setCode('');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      <div className="relative hidden overflow-hidden bg-ink p-12 text-white lg:flex lg:flex-col">
        <div className="absolute -left-20 bottom-0 size-[480px] rounded-full bg-gold/15 blur-3xl" />
        <Link to="/"><Logo light /></Link>
        <div className="relative mt-auto">
          <p className="eyebrow text-gold">Личный кабинет</p>
          <h1 className="display mt-4 text-6xl leading-[1.02]">
            Ваша квартира —<br />
            <em className="text-gold">под контролем</em>
          </h1>
          <ul className="mt-10 flex flex-col gap-4 text-white/65">
            {['График и история платежей', 'Договор, акты и квитанции', 'Чат с вашим менеджером', 'Фото и ход стройки'].map((t, i) => (
              <motion.li key={t} initial={{ opacity: 0, x: -10 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: 0.2 + i * 0.08 }} className="flex items-center gap-3">
                <span className="size-1.5 rounded-full bg-gold" /> {t}
              </motion.li>
            ))}
          </ul>
        </div>
      </div>

      <div className="flex flex-col px-6 py-8 sm:px-12">
        <div className="flex items-center justify-between">
          <Link to="/" className="lg:hidden"><Logo /></Link>
          <Link to="/" className="ml-auto flex items-center gap-1.5 text-sm text-muted hover:text-ink">
            <ArrowLeft className="size-4" /> На сайт
          </Link>
        </div>

        <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center py-12">
          <AnimatePresence mode="wait">
            {step === 'phone' ? (
              <motion.form
                key="phone"
                initial={{ opacity: 0, x: 20 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: -20 }}
                onSubmit={(e) => {
                  e.preventDefault();
                  send();
                }}
                className="flex flex-col gap-5"
              >
                <span className="flex size-12 items-center justify-center rounded-2xl bg-gold-soft text-gold-2">
                  <MessageSquareText className="size-5" />
                </span>
                <div>
                  <h2 className="text-3xl font-semibold tracking-tight">Вход в кабинет</h2>
                  <p className="mt-2 text-muted">Укажите номер, который вы оставляли менеджеру. Мы пришлём SMS с кодом.</p>
                </div>
                <PhoneInput value={phone} onChange={setPhone} autoFocus error={error ?? undefined} />
                <Button type="submit" size="lg" loading={loading} iconRight={<ArrowRight className="size-4" />}>
                  Получить код
                </Button>
                <p className="flex items-start gap-2 text-xs text-muted">
                  <ShieldCheck className="mt-0.5 size-3.5 shrink-0" /> Кабинет доступен покупателям, у которых оформлена бронь или сделка.
                </p>
              </motion.form>
            ) : (
              <motion.div key="code" initial={{ opacity: 0, x: 20 }} animate={{ opacity: 1, x: 0 }} exit={{ opacity: 0, x: -20 }} className="flex flex-col gap-5">
                <button onClick={() => setStep('phone')} className="flex w-fit items-center gap-1.5 text-sm text-muted hover:text-ink">
                  <ArrowLeft className="size-4" /> Изменить номер
                </button>
                <div>
                  <h2 className="text-3xl font-semibold tracking-tight">Введите код</h2>
                  <p className="mt-2 text-muted">
                    Код отправлен на <span className="font-medium text-ink tabular">{formatPhone(normalizePhone(phone))}</span>
                  </p>
                </div>
                <CodeInput
                  value={code}
                  onChange={(v) => {
                    setCode(v);
                    setError(null);
                    if (v.length === 6) verify(v);
                  }}
                  error={!!error}
                  disabled={loading}
                />
                <AnimatePresence>
                  {error && (
                    <motion.p initial={{ opacity: 0, y: -4 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }} className="text-sm text-danger">
                      {error}
                    </motion.p>
                  )}
                </AnimatePresence>
                <Button size="lg" loading={loading} disabled={code.length < 6} onClick={() => verify(code)}>
                  Войти
                </Button>
                <button disabled={resendIn > 0 || loading} onClick={send} className="text-sm text-muted transition hover:text-ink disabled:hover:text-muted">
                  {resendIn > 0 ? `Отправить код повторно через ${resendIn} с` : 'Отправить код повторно'}
                </button>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </div>
    </div>
  );
}

// Шесть ячеек кода с автопереходом и вставкой из буфера.
function CodeInput({ value, onChange, error, disabled }: { value: string; onChange: (v: string) => void; error: boolean; disabled: boolean }) {
  const refs = useRef<(HTMLInputElement | null)[]>([]);
  useEffect(() => {
    refs.current[Math.min(value.length, 5)]?.focus();
  }, [value.length]);

  return (
    <motion.div animate={error ? { x: [0, -8, 8, -6, 6, 0] } : { x: 0 }} transition={{ duration: 0.4 }} className="flex justify-between gap-2">
      {Array.from({ length: 6 }).map((_, i) => (
        <input
          key={i}
          ref={(el) => {
            refs.current[i] = el;
          }}
          inputMode="numeric"
          autoComplete={i === 0 ? 'one-time-code' : 'off'}
          maxLength={6}
          disabled={disabled}
          value={value[i] ?? ''}
          onChange={(e) => {
            const digits = e.target.value.replace(/\D/g, '');
            if (!digits) return;
            const next = (value.slice(0, i) + digits).slice(0, 6);
            onChange(next);
          }}
          onKeyDown={(e) => {
            if (e.key === 'Backspace') {
              e.preventDefault();
              onChange(value.slice(0, Math.max(0, value.length > i ? i : i - 1)));
            }
          }}
          onPaste={(e) => {
            e.preventDefault();
            onChange(e.clipboardData.getData('text').replace(/\D/g, '').slice(0, 6));
          }}
          className={cn(
            'size-12 rounded-xl border bg-white text-center text-xl font-semibold tabular outline-none transition-all sm:size-14',
            error ? 'border-danger' : value[i] ? 'border-ink' : 'border-line',
            'focus:border-gold focus:ring-4 focus:ring-gold/20',
          )}
        />
      ))}
    </motion.div>
  );
}
