import { useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router';
import { motion } from 'motion/react';
import { ArrowLeft, Eye, EyeOff, LockKeyhole, Mail } from 'lucide-react';
import { ApiError } from '@/api/client';
import { auth } from '@/api/endpoints';
import { setSession, useSession } from '@/api/session';
import { Button, Input } from '@/components/ui';
import { Logo } from '@/site/Logo';
import { homeFor } from './access';

export default function CrmLogin() {
  const session = useSession('staff');
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [show, setShow] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  if (session) return <Navigate to={homeFor(session.user.roles)} replace />;

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const response = await auth.login(email.trim(), password);
      if (response.user.roles.every((r) => r === 'Customer')) {
        setError('Это вход для сотрудников. Покупатели входят в личный кабинет по номеру телефона.');
        return;
      }
      setSession('staff', response);
      const from = (location.state as { from?: string } | null)?.from;
      navigate(from && from !== '/crm' ? from : homeFor(response.user.roles), { replace: true });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Не удалось войти');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-ink px-4">
      <div className="absolute left-1/2 top-1/3 size-[700px] -translate-x-1/2 -translate-y-1/2 rounded-full bg-gold/10 blur-3xl" />
      <div className="absolute inset-0 bg-[radial-gradient(rgb(255_255_255/0.05)_1px,transparent_1px)] [background-size:24px_24px]" />
      <motion.div initial={{ opacity: 0, y: 20, scale: 0.98 }} animate={{ opacity: 1, y: 0, scale: 1 }} transition={{ duration: 0.5, ease: [0.22, 1, 0.36, 1] }} className="relative w-full max-w-[420px]">
        <div className="mb-8 flex justify-center">
          <Logo light />
        </div>
        <form onSubmit={submit} className="rounded-[28px] bg-white p-8 shadow-lift">
          <h1 className="text-2xl font-semibold tracking-tight">Вход в CRM</h1>
          <p className="mt-1 text-sm text-muted">Для менеджеров, бухгалтерии и администраторов</p>
          <div className="mt-7 flex flex-col gap-4">
            <Input label="Почта" type="email" autoComplete="username" prefix={<Mail className="size-4" />} value={email} onChange={(e) => setEmail(e.target.value)} required autoFocus />
            <Input
              label="Пароль"
              type={show ? 'text' : 'password'}
              autoComplete="current-password"
              prefix={<LockKeyhole className="size-4" />}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              suffix={
                <button type="button" onClick={() => setShow((v) => !v)} className="text-muted hover:text-ink" aria-label={show ? 'Скрыть пароль' : 'Показать пароль'}>
                  {show ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </button>
              }
            />
            {error && (
              <motion.p initial={{ opacity: 0, y: -4 }} animate={{ opacity: 1, y: 0, x: [0, -6, 6, -4, 4, 0] }} className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger">
                {error}
              </motion.p>
            )}
            <Button type="submit" size="lg" loading={loading} className="mt-2">
              Войти
            </Button>
          </div>
        </form>
        <Link to="/" className="mt-6 flex items-center justify-center gap-1.5 text-sm text-white/50 hover:text-white">
          <ArrowLeft className="size-4" /> На сайт
        </Link>
      </motion.div>
    </div>
  );
}
