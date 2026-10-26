import { useEffect, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, Search } from 'lucide-react';
import { ApiError } from '@/api/client';
import { crm } from '@/api/endpoints';
import type { Apartment, Currency, CustomerListItem, CustomerResponse, DealResponse, DealStage } from '@/api/types';
import { Button, Input, Modal, PhoneInput, Segmented, Select, Spinner, StatusBadge, Textarea, useToast } from '@/components/ui';
import { cn } from '@/lib/cn';
import { area, dealStageLabel, formatPhone, money, normalizePhone, roomsShort } from '@/lib/format';
import { useAllApartments, useMainBuilding } from '@/site/hooks';

function useDebounced<T>(value: T, ms = 300) {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return v;
}

// --- Клиент ---
export function CustomerFormModal({
  open,
  onClose,
  onCreated,
  lead,
}: {
  open: boolean;
  onClose: () => void;
  onCreated?: (c: CustomerResponse) => void;
  lead?: { id: string; name: string; phone: string } | null;
}) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [fullName, setFullName] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [passport, setPassport] = useState('');
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!open) return;
    setFullName(lead?.name ?? '');
    setPhone(lead?.phone.replace(/^\+992/, '') ?? '');
    setEmail('');
    setPassport('');
    setError(null);
  }, [open, lead]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      const created = await crm.createCustomer({
        fullName: fullName.trim(),
        phone: normalizePhone(phone),
        email: email.trim() || null,
        passportData: passport.trim() || null,
        leadId: lead?.id ?? null,
      });
      toast.success(lead ? 'Клиент создан, заявка закрыта' : 'Клиент создан');
      queryClient.invalidateQueries({ queryKey: ['crm'] });
      onCreated?.(created);
      onClose();
    } catch (err) {
      setError(err instanceof ApiError ? err : new ApiError(0, 'Ошибка'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={lead ? 'Клиент из заявки' : 'Новый клиент'}
      subtitle={lead ? 'Заявка закроется, её брони перейдут на клиента' : undefined}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button type="submit" form="customer-form" loading={loading}>Создать клиента</Button>
        </>
      }
    >
      <form id="customer-form" onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <Input className="sm:col-span-2" label="ФИО" value={fullName} onChange={(e) => setFullName(e.target.value)} required autoFocus error={error?.field('fullName')} />
        <PhoneInput value={phone} onChange={setPhone} error={error?.field('phone')} />
        <Input label="Почта" type="email" value={email} onChange={(e) => setEmail(e.target.value)} error={error?.field('email')} />
        <Textarea
          className="sm:col-span-2"
          label="Паспортные данные"
          hint="Хранятся в зашифрованном виде, видны только менеджерам и администратору"
          value={passport}
          onChange={(e) => setPassport(e.target.value)}
          error={error?.field('passportData')}
        />
        {error && !Object.keys(error.fieldErrors).length && <p className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger sm:col-span-2">{error.message}</p>}
      </form>
    </Modal>
  );
}

// --- Выбор клиента ---
export function CustomerPicker({ value, onChange }: { value: CustomerListItem | null; onChange: (c: CustomerListItem | null) => void }) {
  const [search, setSearch] = useState('');
  const debounced = useDebounced(search);
  const q = useQuery({ queryKey: ['crm', 'customers', debounced, 1], queryFn: () => crm.customers(debounced || undefined) });

  if (value)
    return (
      <div className="flex items-center justify-between rounded-xl border border-gold bg-gold-soft/50 px-4 py-3">
        <div>
          <p className="font-medium">{value.fullName}</p>
          <p className="text-sm text-muted tabular">{formatPhone(value.phone)}</p>
        </div>
        <button onClick={() => onChange(null)} className="text-sm text-muted hover:text-ink">Изменить</button>
      </div>
    );

  return (
    <div className="rounded-xl border border-line">
      <div className="relative border-b border-line">
        <Search className="absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-muted" />
        <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Имя или телефон" className="h-11 w-full rounded-t-xl bg-transparent pl-10 pr-4 text-sm outline-none" autoFocus />
      </div>
      <div className="scrollbar-thin max-h-56 overflow-y-auto p-1">
        {q.isLoading ? (
          <div className="flex justify-center py-6 text-muted"><Spinner /></div>
        ) : q.data?.items.length ? (
          q.data.items.map((c) => (
            <button key={c.id} type="button" onClick={() => onChange(c)} className="flex w-full items-center justify-between rounded-lg px-3 py-2.5 text-left transition-colors hover:bg-paper">
              <span className="text-sm font-medium">{c.fullName}</span>
              <span className="text-xs text-muted tabular">{formatPhone(c.phone)}</span>
            </button>
          ))
        ) : (
          <p className="py-6 text-center text-sm text-muted">Никого не нашли</p>
        )}
      </div>
    </div>
  );
}

// --- Выбор квартиры ---
export function ApartmentPicker({
  value,
  onChange,
  statuses = ['Free'],
}: {
  value: Apartment | null;
  onChange: (a: Apartment | null) => void;
  statuses?: Apartment['status'][];
}) {
  const { data: building } = useMainBuilding();
  const { data, isLoading } = useAllApartments(building?.id);
  const [search, setSearch] = useState('');
  const list = useMemo(
    () =>
      (data ?? [])
        .filter((a) => statuses.includes(a.status))
        .filter((a) => !search || String(a.number).includes(search.trim()))
        .sort((a, b) => a.number - b.number),
    [data, search, statuses],
  );

  if (value)
    return (
      <div className="flex items-center justify-between rounded-xl border border-gold bg-gold-soft/50 px-4 py-3">
        <div>
          <p className="font-medium">Квартира №{value.number}</p>
          <p className="text-sm text-muted">
            {roomsShort(value.rooms)} · {area(value.area)} · {value.floorNumber} этаж · {money(value.price)}
          </p>
        </div>
        <button onClick={() => onChange(null)} className="text-sm text-muted hover:text-ink">Изменить</button>
      </div>
    );

  return (
    <div className="rounded-xl border border-line">
      <div className="relative border-b border-line">
        <Search className="absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-muted" />
        <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Номер квартиры" inputMode="numeric" className="h-11 w-full rounded-t-xl bg-transparent pl-10 pr-4 text-sm outline-none" />
      </div>
      <div className="scrollbar-thin grid max-h-64 grid-cols-2 gap-1 overflow-y-auto p-1 sm:grid-cols-3">
        {isLoading ? (
          <div className="col-span-full flex justify-center py-6 text-muted"><Spinner /></div>
        ) : list.length ? (
          list.map((a) => (
            <button key={a.id} type="button" onClick={() => onChange(a)} className="rounded-lg border border-transparent px-3 py-2 text-left transition-all hover:border-line hover:bg-paper">
              <span className="flex items-center justify-between">
                <span className="font-display text-lg">{a.number}</span>
                <span className="text-[11px] text-muted">{a.floorNumber} эт.</span>
              </span>
              <span className="block text-xs text-muted">{roomsShort(a.rooms)} · {area(a.area)}</span>
              <span className="block text-xs font-medium tabular">{money(a.price)}</span>
            </button>
          ))
        ) : (
          <p className="col-span-full py-6 text-center text-sm text-muted">Нет подходящих квартир</p>
        )}
      </div>
    </div>
  );
}

// --- Сделка ---
export function DealFormModal({
  open,
  onClose,
  onCreated,
  customer: presetCustomer,
  apartment: presetApartment,
}: {
  open: boolean;
  onClose: () => void;
  onCreated?: (d: DealResponse) => void;
  customer?: CustomerListItem | null;
  apartment?: Apartment | null;
}) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [customer, setCustomer] = useState<CustomerListItem | null>(null);
  const [apartment, setApartment] = useState<Apartment | null>(null);
  const [stage, setStage] = useState<DealStage>('Booking');
  const [currency, setCurrency] = useState<Currency>('TJS');
  const [discount, setDiscount] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!open) return;
    setCustomer(presetCustomer ?? null);
    setApartment(presetApartment ?? null);
    setStage('Booking');
    setCurrency('TJS');
    setDiscount('');
    setReason('');
    setError(null);
  }, [open, presetCustomer, presetApartment]);

  const discountValue = Number(discount.replace(',', '.')) || 0;

  const submit = async () => {
    if (!customer || !apartment) return;
    setLoading(true);
    setError(null);
    try {
      const deal = await crm.createDeal({
        apartmentId: apartment.id,
        customerId: customer.id,
        stage,
        currency,
        discount: discountValue || null,
        discountReason: discountValue ? reason.trim() || null : null,
      });
      toast.success(`Сделка по квартире №${deal.apartmentNumber} создана`);
      queryClient.invalidateQueries({ queryKey: ['crm'] });
      queryClient.invalidateQueries({ queryKey: ['apartments'] });
      onCreated?.(deal);
      onClose();
    } catch (err) {
      setError(err instanceof ApiError ? err : new ApiError(0, 'Ошибка'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="lg"
      title="Новая сделка"
      subtitle="Цена фиксируется в сделке в момент создания"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button onClick={submit} loading={loading} disabled={!customer || !apartment || (discountValue > 0 && !reason.trim())}>
            Создать сделку
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-5">
        <Step n={1} title="Клиент" done={!!customer}>
          <CustomerPicker value={customer} onChange={setCustomer} />
        </Step>
        <Step n={2} title="Квартира" done={!!apartment}>
          <ApartmentPicker value={apartment} onChange={setApartment} statuses={['Free', 'Booked', 'OnRequest']} />
          {apartment?.status === 'Booked' && <p className="mt-2 text-xs text-muted">Квартира в брони: сделка создастся, только если бронь оформлена на этого клиента.</p>}
        </Step>
        <Step n={3} title="Условия" done={false}>
          <div className="grid gap-4 sm:grid-cols-2">
            <Select label="Этап" value={stage} onChange={(e) => setStage(e.target.value as DealStage)}>
              {(['Lead', 'Showing', 'Booking', 'Contract'] as DealStage[]).map((s) => (
                <option key={s} value={s}>{dealStageLabel[s]}</option>
              ))}
            </Select>
            <div className="flex flex-col gap-1.5">
              <span className="text-[13px] font-medium text-ink/80">Валюта договора</span>
              <Segmented value={currency} onChange={setCurrency} options={[{ value: 'TJS', label: 'Сомони' }, { value: 'USD', label: 'Доллары' }]} />
            </div>
            <Input label="Скидка" inputMode="decimal" value={discount} onChange={(e) => setDiscount(e.target.value)} suffix={currency === 'USD' ? '$' : 'сом.'} error={error?.field('discount')} />
            <Input label="Причина скидки" value={reason} onChange={(e) => setReason(e.target.value)} disabled={!discountValue} error={error?.field('discountReason')} />
          </div>
          {apartment && (
            <p className="mt-3 text-sm text-muted">
              Цена квартиры: <span className="font-semibold text-ink tabular">{money(apartment.price)}</span>
              {currency === 'USD' && ' — будет пересчитана в доллары по курсу на сегодня'}
            </p>
          )}
        </Step>
        {error && <p className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger">{error.message}</p>}
      </div>
    </Modal>
  );
}

function Step({ n, title, done, children }: { n: number; title: string; done: boolean; children: React.ReactNode }) {
  return (
    <div>
      <p className="mb-2 flex items-center gap-2 text-sm font-semibold">
        <span className={cn('flex size-6 items-center justify-center rounded-full text-xs', done ? 'bg-free text-white' : 'bg-ink text-white')}>
          {done ? <Check className="size-3.5" /> : n}
        </span>
        {title}
      </p>
      {children}
    </div>
  );
}

// --- Бронь на клиента (менеджер) ---
export function BookForCustomerModal({ open, onClose, apartment }: { open: boolean; onClose: () => void; apartment: Apartment | null }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [customer, setCustomer] = useState<CustomerListItem | null>(null);
  const [comment, setComment] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setCustomer(null);
      setComment('');
      setError(null);
    }
  }, [open]);

  const submit = async () => {
    if (!apartment || !customer) return;
    setLoading(true);
    setError(null);
    try {
      await crm.book(apartment.id, { customerId: customer.id, comment: comment.trim() || null });
      toast.success(`Квартира №${apartment.number} забронирована на 48 часов`);
      queryClient.invalidateQueries({ queryKey: ['crm'] });
      queryClient.invalidateQueries({ queryKey: ['apartments'] });
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Ошибка');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={apartment ? `Бронь квартиры №${apartment.number}` : 'Бронь'}
      subtitle="Бронь на 48 часов, её можно продлить"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button onClick={submit} loading={loading} disabled={!customer}>Забронировать</Button>
        </>
      }
    >
      {apartment && (
        <div className="mb-5 flex items-center justify-between rounded-2xl bg-paper p-4">
          <div>
            <p className="font-display text-3xl">{apartment.number}</p>
            <p className="text-sm text-muted">{roomsShort(apartment.rooms)} · {area(apartment.area)} · {apartment.floorNumber} этаж</p>
          </div>
          <div className="text-right">
            <StatusBadge status={apartment.status} />
            <p className="mt-1 font-semibold tabular">{money(apartment.price)}</p>
          </div>
        </div>
      )}
      <p className="mb-2 text-sm font-semibold">Клиент</p>
      <CustomerPicker value={customer} onChange={setCustomer} />
      <Textarea className="mt-4" label="Комментарий" value={comment} onChange={(e) => setComment(e.target.value)} />
      {error && <p className="mt-4 rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger">{error}</p>}
    </Modal>
  );
}
