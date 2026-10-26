import { useState } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { Check, Clock, Mail, MapPin, Phone } from 'lucide-react';
import { ApiError } from '@/api/client';
import { catalog } from '@/api/endpoints';
import { Button, Input, PhoneInput, Reveal, Textarea } from '@/components/ui';
import { contacts } from '@/config';
import { normalizePhone } from '@/lib/format';
import { useMainBuilding } from './hooks';

export default function ContactsPage() {
  const { data: building } = useMainBuilding();
  const items = [
    { icon: Phone, label: 'Телефон', value: contacts.phone, href: `tel:${contacts.phone.replace(/\s/g, '')}` },
    { icon: Mail, label: 'Почта', value: contacts.email, href: `mailto:${contacts.email}` },
    { icon: MapPin, label: 'Адрес дома', value: building?.address ?? contacts.officeAddress },
    { icon: Clock, label: 'Офис продаж', value: contacts.hours },
  ];
  return (
    <div className="container-x pb-24 pt-28">
      <Reveal>
        <p className="eyebrow">Контакты</p>
        <h1 className="display mt-2 max-w-3xl text-5xl sm:text-6xl">Приезжайте в офис продаж или напишите нам</h1>
      </Reveal>
      <div className="mt-14 grid gap-8 lg:grid-cols-[1fr_480px]">
        <div className="grid gap-4 sm:grid-cols-2">
          {items.map((it, i) => (
            <Reveal key={it.label} delay={i * 0.06}>
              <a
                href={it.href}
                className="group flex h-full flex-col rounded-2xl border border-line/80 bg-white p-6 transition-all duration-300 hover:-translate-y-1 hover:shadow-lift"
              >
                <span className="flex size-11 items-center justify-center rounded-xl bg-gold-soft text-gold-2 transition-all duration-300 group-hover:rotate-[-6deg] group-hover:bg-ink group-hover:text-gold">
                  <it.icon className="size-5" />
                </span>
                <span className="mt-6 text-xs uppercase tracking-widest text-muted">{it.label}</span>
                <span className="mt-1 text-lg font-semibold tabular">{it.value}</span>
              </a>
            </Reveal>
          ))}
          <Reveal className="sm:col-span-2" delay={0.2}>
            <MapCard />
          </Reveal>
        </div>
        <Reveal delay={0.1}>
          <ContactForm />
        </Reveal>
      </div>
    </div>
  );
}

function MapCard() {
  return (
    <div className="relative h-64 overflow-hidden rounded-2xl bg-ink">
      <svg className="absolute inset-0 h-full w-full opacity-30" preserveAspectRatio="none" viewBox="0 0 400 200">
        {Array.from({ length: 14 }).map((_, i) => (
          <path key={i} d={`M0 ${i * 16} Q 200 ${i * 16 + (i % 2 ? 30 : -30)} 400 ${i * 16 + 10}`} stroke="#c9a46a" strokeWidth="0.5" fill="none" />
        ))}
        <path d="M0 120 L400 80" stroke="#fff" strokeWidth="6" strokeOpacity="0.2" />
        <path d="M160 0 L220 200" stroke="#fff" strokeWidth="4" strokeOpacity="0.15" />
      </svg>
      <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2">
        <span className="absolute inset-0 -m-4 animate-ping rounded-full bg-gold/30" />
        <span className="relative flex size-12 items-center justify-center rounded-full bg-gold text-ink shadow-lift">
          <MapPin className="size-5" />
        </span>
      </div>
      <p className="absolute bottom-4 left-5 text-sm text-white/70">Sitora Residence</p>
    </div>
  );
}

function ContactForm() {
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [comment, setComment] = useState('');
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (phone.length < 9) {
      setError(new ApiError(400, 'Введите номер телефона полностью', { phone: ['Введите номер телефона полностью'] }));
      return;
    }
    setLoading(true);
    try {
      await catalog.createLead({ name: name.trim(), phone: normalizePhone(phone), source: 'Website', comment: comment.trim() || null });
      setDone(true);
    } catch (err) {
      setError(err instanceof ApiError ? err : new ApiError(0, 'Не удалось отправить'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="rounded-[28px] bg-ink p-8 text-white shadow-lift">
      <AnimatePresence mode="wait">
        {done ? (
          <motion.div key="ok" initial={{ opacity: 0, scale: 0.96 }} animate={{ opacity: 1, scale: 1 }} className="flex min-h-[420px] flex-col items-center justify-center text-center">
            <motion.span initial={{ scale: 0 }} animate={{ scale: 1 }} transition={{ type: 'spring', bounce: 0.5 }} className="flex size-16 items-center justify-center rounded-full bg-gold text-ink">
              <Check className="size-8" />
            </motion.span>
            <h3 className="display mt-6 text-3xl">Спасибо!</h3>
            <p className="mt-2 text-white/60">Перезвоним в рабочее время.</p>
            <Button variant="outline-light" className="mt-6" onClick={() => { setDone(false); setName(''); setPhone(''); setComment(''); }}>
              Отправить ещё
            </Button>
          </motion.div>
        ) : (
          <motion.form key="form" onSubmit={submit} exit={{ opacity: 0 }} className="flex flex-col gap-4 [&_label]:text-white/70">
            <h2 className="display text-3xl">Обратный звонок</h2>
            <p className="-mt-2 mb-2 text-sm text-white/50">Ответим на вопросы о квартирах, рассрочке и документах.</p>
            <Input
              label="Имя"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              error={error?.field('name')}
              className="[&_input]:border-white/15 [&_input]:bg-white/5 [&_input]:text-white"
            />
            <PhoneInput value={phone} onChange={setPhone} error={error?.field('phone')} dark />
            <Textarea
              label="Вопрос"
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              className="[&_textarea]:border-white/15 [&_textarea]:bg-white/5 [&_textarea]:text-white"
            />
            {error && !error.field('name') && !error.field('phone') && <p className="rounded-xl bg-danger/20 px-4 py-3 text-sm text-[#ffb4ad]">{error.message}</p>}
            <Button type="submit" variant="gold" size="lg" loading={loading}>
              Жду звонка
            </Button>
          </motion.form>
        )}
      </AnimatePresence>
    </div>
  );
}
