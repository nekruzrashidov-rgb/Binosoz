import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { AnimatePresence, motion } from 'motion/react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, BadgeCheck, CalendarClock, Check, ChevronRight, Clock, Copy, Maximize2, Ruler, Share2 } from 'lucide-react';
import { ApiError } from '@/api/client';
import { catalog } from '@/api/endpoints';
import type { Apartment, BookingResponse } from '@/api/types';
import { Button, ErrorState, Input, KeyValue, Modal, PhoneInput, Skeleton, StatusBadge, Textarea, useToast } from '@/components/ui';
import { area, dateTime, money, normalizePhone, roomsLabel, timeLeft } from '@/lib/format';
import { FloorPlan } from './FloorPlan';
import { useRate } from './hooks';
import { useLeadModal } from './LeadModal';

export default function ApartmentPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const apartment = useQuery({ queryKey: ['apartment', id], queryFn: () => catalog.apartment(id) });
  const a = apartment.data;
  const floorApartments = useQuery({
    queryKey: ['apartments', a?.floorId],
    queryFn: () => catalog.apartments(a!.floorId),
    enabled: !!a,
  });
  const rate = useRate();
  const openLead = useLeadModal();
  const toast = useToast();
  const [booking, setBooking] = useState(false);

  if (apartment.error) return <div className="pt-32"><ErrorState error={apartment.error} onRetry={() => apartment.refetch()} /></div>;

  const usd = a && rate.data ? Math.round(a.price / rate.data.rateToTjs) : null;

  const share = async () => {
    const url = window.location.href;
    try {
      if (navigator.share) await navigator.share({ title: `Квартира ${a?.number}`, url });
      else {
        await navigator.clipboard.writeText(url);
        toast.success('Ссылка скопирована');
      }
    } catch {
      /* пользователь закрыл окно */
    }
  };

  return (
    <div className="container-x pb-24 pt-28">
      <nav className="flex flex-wrap items-center gap-1.5 text-sm text-muted">
        <Link to="/" className="hover:text-ink">Главная</Link>
        <ChevronRight className="size-3.5" />
        {a ? (
          <>
            <Link to={`/floors/${a.floorId}`} className="hover:text-ink">Этаж {a.floorNumber}</Link>
            <ChevronRight className="size-3.5" />
            <span className="text-ink">Квартира {a.number}</span>
          </>
        ) : (
          <Skeleton className="h-4 w-32" />
        )}
      </nav>

      <div className="mt-6 grid gap-10 lg:grid-cols-[1fr_420px]">
        <div>
          <div className="flex flex-wrap items-center gap-3">
            {a ? <StatusBadge status={a.status} /> : <Skeleton className="h-6 w-24" />}
            {a && <span className="text-sm text-muted">Этаж {a.floorNumber} · позиция {a.planSlot + 1}</span>}
          </div>
          <h1 className="display mt-4 text-5xl sm:text-7xl">
            {a ? (
              <>
                {roomsLabel(a.rooms)} <span className="text-gold">№{a.number}</span>
              </>
            ) : (
              <Skeleton className="h-16 w-3/4" />
            )}
          </h1>

          <div className="mt-8 grid grid-cols-3 gap-3">
            <Spec icon={<Maximize2 className="size-4" />} label="Площадь" value={a ? area(a.area) : null} />
            <Spec icon={<Ruler className="size-4" />} label="Комнат" value={a ? (a.rooms === 0 ? 'Студия' : a.rooms) : null} />
            <Spec icon={<BadgeCheck className="size-4" />} label="Этаж" value={a ? a.floorNumber : null} />
          </div>

          <div className="mt-8 overflow-hidden rounded-[28px] border border-line/80 bg-white p-4 shadow-soft sm:p-6">
            <div className="mb-4 flex items-center justify-between">
              <p className="text-sm font-medium">Расположение на этаже</p>
              {a && (
                <Link to={`/floors/${a.floorId}`} className="text-sm text-gold-2 hover:text-ink">
                  Весь этаж →
                </Link>
              )}
            </div>
            {floorApartments.data ? (
              <FloorPlan apartments={floorApartments.data.items} hoveredId={id} onSelect={(x) => x.id !== id && navigate(`/apartments/${x.id}`)} />
            ) : (
              <Skeleton className="aspect-[1000/520] w-full" />
            )}
          </div>
        </div>

        <aside className="lg:sticky lg:top-24 lg:self-start">
          <motion.div initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} className="rounded-[28px] bg-ink p-7 text-white shadow-lift">
            <p className="text-sm text-white/50">Стоимость</p>
            {a ? (
              a.status === 'OnRequest' ? (
                <p className="display mt-1 text-4xl">По запросу</p>
              ) : (
                <>
                  <p className="display mt-1 text-5xl tabular">{money(a.price, a.currency)}</p>
                  <div className="mt-2 flex flex-wrap gap-x-4 text-sm text-white/50">
                    <span className="tabular">{money(a.pricePerM2)} / м²</span>
                    {usd !== null && <span className="tabular">≈ ${usd.toLocaleString('ru-RU')} по курсу НБТ</span>}
                  </div>
                </>
              )
            ) : (
              <Skeleton className="mt-2 h-12 w-48 bg-white/10" />
            )}

            <div className="mt-7 flex flex-col gap-2.5">
              {a?.status === 'Free' && (
                <Button variant="gold" size="lg" icon={<CalendarClock className="size-4" />} onClick={() => setBooking(true)}>
                  Забронировать на 48 часов
                </Button>
              )}
              <Button
                variant="outline-light"
                size="lg"
                onClick={() => openLead({ apartmentId: a?.id, title: a ? `Квартира №${a.number}, ${roomsLabel(a.rooms).toLowerCase()}` : undefined })}
              >
                {a?.status === 'OnRequest' ? 'Узнать цену' : 'Задать вопрос'}
              </Button>
              {a && (a.status === 'Booked' || a.status === 'Sold') && (
                <p className="rounded-2xl bg-white/5 px-4 py-3 text-sm text-white/60">
                  {a.status === 'Booked' ? 'Квартира забронирована. Оставьте заявку — сообщим, если бронь снимут.' : 'Квартира продана. Посмотрите похожие на этом этаже.'}
                </p>
              )}
            </div>

            <div className="mt-7 divide-y divide-white/10 border-t border-white/10 text-white [&_span:first-child]:text-white/50">
              {a && <KeyValue label="Цена за м²" value={money(a.pricePerM2)} />}
              {a && <KeyValue label="Валюта договора" value="TJS или USD" />}
              {rate.data && <KeyValue label="Курс USD" value={`${rate.data.rateToTjs} сом.`} />}
            </div>

            <button onClick={share} className="mt-6 flex w-full items-center justify-center gap-2 text-sm text-white/55 transition-colors hover:text-white">
              <Share2 className="size-4" /> Поделиться квартирой
            </button>
          </motion.div>

          <Link to={a ? `/floors/${a.floorId}` : '/'} className="mt-4 flex items-center gap-2 text-sm text-muted hover:text-ink">
            <ArrowLeft className="size-4" /> К плану этажа
          </Link>
        </aside>
      </div>

      {a && <BookingModal apartment={a} open={booking} onClose={() => setBooking(false)} />}
    </div>
  );
}

function Spec({ icon, label, value }: { icon: React.ReactNode; label: string; value: React.ReactNode | null }) {
  return (
    <div className="group rounded-2xl border border-line/80 bg-white p-4 transition-all duration-300 hover:-translate-y-0.5 hover:shadow-soft sm:p-5">
      <span className="flex size-8 items-center justify-center rounded-lg bg-paper text-gold-2 transition-colors group-hover:bg-ink group-hover:text-gold">{icon}</span>
      <p className="mt-4 text-xs uppercase tracking-wider text-muted">{label}</p>
      <p className="mt-1 text-2xl font-semibold tabular">{value ?? <Skeleton className="h-7 w-16" />}</p>
    </div>
  );
}

function BookingModal({ apartment, open, onClose }: { apartment: Apartment; open: boolean; onClose: () => void }) {
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [comment, setComment] = useState('');
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<BookingResponse | null>(null);
  const toast = useToast();

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (phone.length < 9) {
      setError(new ApiError(400, 'Введите номер телефона полностью', { phone: ['Введите номер телефона полностью'] }));
      return;
    }
    setLoading(true);
    try {
      const booking = await catalog.book(apartment.id, { name: name.trim(), phone: normalizePhone(phone), comment: comment.trim() || null });
      setResult(booking);
      // Статус квартиры изменился — обновляем каталог, этаж и 3D.
      queryClient.invalidateQueries({ queryKey: ['apartment', apartment.id] });
      queryClient.invalidateQueries({ queryKey: ['apartments'] });
      queryClient.invalidateQueries({ queryKey: ['floors'] });
      queryClient.invalidateQueries({ queryKey: ['buildings'] });
    } catch (err) {
      setError(err instanceof ApiError ? err : new ApiError(0, 'Не удалось забронировать'));
    } finally {
      setLoading(false);
    }
  };

  const close = () => {
    onClose();
    setTimeout(() => {
      setResult(null);
      setError(null);
    }, 300);
  };

  return (
    <Modal open={open} onClose={close} title={result ? undefined : `Бронь квартиры №${apartment.number}`} subtitle={result ? undefined : 'Квартира будет закреплена за вами на 48 часов'}>
      <AnimatePresence mode="wait">
        {result ? (
          <motion.div key="ok" initial={{ opacity: 0, scale: 0.96 }} animate={{ opacity: 1, scale: 1 }} className="flex flex-col items-center py-6 text-center">
            <motion.div initial={{ scale: 0, rotate: -30 }} animate={{ scale: 1, rotate: 0 }} transition={{ type: 'spring', bounce: 0.5 }} className="flex size-16 items-center justify-center rounded-full bg-free text-white">
              <Check className="size-8" strokeWidth={2.5} />
            </motion.div>
            <h3 className="display mt-5 text-3xl">Квартира забронирована</h3>
            <p className="mt-2 max-w-sm text-muted">Менеджер свяжется с вами, чтобы договориться о показе и договоре.</p>
            <div className="mt-6 w-full rounded-2xl bg-paper p-4 text-left">
              <KeyValue label="Квартира" value={`№${result.apartmentNumber}`} />
              <KeyValue label="Бронь до" value={dateTime(result.expiresAt)} />
              <KeyValue label="Осталось" value={<span className="inline-flex items-center gap-1.5 text-free"><Clock className="size-3.5" /> {timeLeft(result.expiresAt)}</span>} />
              <KeyValue
                label="Номер брони"
                value={
                  <button
                    className="inline-flex items-center gap-1.5 text-xs text-muted hover:text-ink"
                    onClick={() => navigator.clipboard.writeText(result.id).then(() => toast.success('Номер брони скопирован'))}
                  >
                    {result.id.slice(0, 8).toUpperCase()} <Copy className="size-3" />
                  </button>
                }
              />
            </div>
            <Button className="mt-6 w-full" size="lg" onClick={close}>
              Готово
            </Button>
          </motion.div>
        ) : (
          <motion.form key="form" onSubmit={submit} exit={{ opacity: 0 }} className="flex flex-col gap-4">
            <div className="flex items-center gap-4 rounded-2xl bg-paper p-4">
              <span className="display text-4xl text-gold-2">{apartment.number}</span>
              <div className="flex-1 text-sm">
                <p className="font-medium">{roomsLabel(apartment.rooms)}, {area(apartment.area)}</p>
                <p className="text-muted">Этаж {apartment.floorNumber}</p>
              </div>
              <p className="font-semibold tabular">{money(apartment.price, apartment.currency)}</p>
            </div>
            <Input label="Ваше имя" value={name} onChange={(e) => setName(e.target.value)} required autoFocus error={error?.field('name')} />
            <PhoneInput value={phone} onChange={setPhone} error={error?.field('phone')} />
            <Textarea label="Комментарий" placeholder="Когда удобно приехать на показ?" value={comment} onChange={(e) => setComment(e.target.value)} />
            <AnimatePresence>
              {error && !error.field('name') && !error.field('phone') && (
                <motion.p initial={{ opacity: 0, y: -4 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }} className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger">
                  {error.message}
                </motion.p>
              )}
            </AnimatePresence>
            <Button type="submit" size="lg" variant="gold" loading={loading}>
              Забронировать бесплатно
            </Button>
            <p className="text-center text-xs text-muted">Бронь бесплатная. Если не успеете подписать договор за 48 часов, она снимется автоматически.</p>
          </motion.form>
        )}
      </AnimatePresence>
    </Modal>
  );
}
