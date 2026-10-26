import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { AnimatePresence, motion } from 'motion/react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Ban, CalendarPlus, CalendarRange, FileText, MessageCircle, Plus, Upload, Wallet } from 'lucide-react';
import { ApiError } from '@/api/client';
import { catalog, crm } from '@/api/endpoints';
import { DEAL_STAGES, type Currency, type DealStage, type DocumentType, type PaymentMethod, type PaymentResponse } from '@/api/types';
import { Badge, Button, Card, ConfirmDialog, EmptyState, ErrorState, FileDrop, Input, KeyValue, Modal, Segmented, Select, Skeleton, Tabs, Textarea, useToast } from '@/components/ui';
import { area, dateShort, dealStageLabel, documentTypeLabel, money, paymentMethodLabel, roomsLabel, todayIso } from '@/lib/format';
import { ChatPanel } from '@/shared/ChatPanel';
import { DocumentsList } from '@/shared/DocumentsList';
import { ScheduleView } from '@/shared/ScheduleView';
import { StageTimeline } from '@/shared/StageTimeline';
import { ROLES, useStaff } from '../access';
import { Guard } from '../Guard';

type Tab = 'schedule' | 'documents' | 'chat';

export default function DealDetail() {
  return (
    <Guard roles={ROLES.dealStaff}>
      <Content />
    </Guard>
  );
}

function Content() {
  const { id = '' } = useParams();
  const staff = useStaff();
  const queryClient = useQueryClient();
  const toast = useToast();
  const sales = staff.can(ROLES.sales);
  // Карточку сделки сервер отдаёт менеджеру и админу; бухгалтер работает с графиком, платежами и документами.
  const deal = useQuery({ queryKey: ['crm', 'deal', id], queryFn: () => crm.deal(id), enabled: sales });
  const d = deal.data;
  const apartment = useQuery({ queryKey: ['apartment', d?.apartmentId], queryFn: () => catalog.apartment(d!.apartmentId), enabled: !!d });
  const [tab, setTab] = useState<Tab>('schedule');
  const [stageChange, setStageChange] = useState<DealStage | null>(null);
  const [busy, setBusy] = useState(false);

  if (deal.error) return <ErrorState error={deal.error} onRetry={() => deal.refetch()} />;

  const backward = (to: DealStage) => !!d && (to === 'Cancelled' || DEAL_STAGES.indexOf(to) < DEAL_STAGES.indexOf(d.stage));

  const changeStage = async (to: DealStage, reason?: string) => {
    setBusy(true);
    try {
      await crm.changeStage(id, to, reason);
      toast.success(`Этап: ${dealStageLabel[to]}`);
      setStageChange(null);
      queryClient.invalidateQueries({ queryKey: ['crm'] });
      queryClient.invalidateQueries({ queryKey: ['apartment'] });
    } catch (e) {
      toast.error(e);
    } finally {
      setBusy(false);
    }
  };

  const final = d?.stage === 'Completed' || d?.stage === 'Cancelled';

  return (
    <>
      <Link to={sales ? '/crm/deals' : '/crm/reports'} className="inline-flex items-center gap-1.5 text-sm text-muted hover:text-ink">
        <ArrowLeft className="size-4" /> {sales ? 'Сделки' : 'Отчёты'}
      </Link>

      <motion.section initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} className="mt-4 grid gap-6 xl:grid-cols-[1fr_340px]">
        <Card className="p-6 sm:p-8">
          <div className="flex flex-wrap items-start justify-between gap-6">
            <div>
              <p className="eyebrow">Сделка</p>
              <h1 className="mt-2 text-3xl font-semibold tracking-tight">
                {d ? (
                  <>
                    Квартира <span className="font-display text-4xl text-gold-2">{d.apartmentNumber}</span>
                  </>
                ) : sales ? (
                  <Skeleton className="h-9 w-64" />
                ) : (
                  'Платежи и документы'
                )}
              </h1>
              {d && (
                <p className="mt-1 text-muted">
                  <Link to={`/crm/customers/${d.customerId}`} className="font-medium text-ink hover:text-gold-2">{d.customerName}</Link> · {d.buildingName}
                </p>
              )}
            </div>
            {d && (
              <div className="flex items-center gap-2">
                <Badge tone={d.stage === 'Cancelled' ? 'red' : d.stage === 'Completed' ? 'green' : 'gold'}>{dealStageLabel[d.stage]}</Badge>
                {!final && (
                  <Button variant="ghost" size="sm" icon={<Ban className="size-3.5" />} className="text-danger hover:bg-danger-soft" onClick={() => setStageChange('Cancelled')}>
                    Отменить
                  </Button>
                )}
              </div>
            )}
          </div>
          {d && (
            <div className="mt-8">
              <StageTimeline stage={d.stage} onPick={final ? undefined : (s) => (backward(s) ? setStageChange(s) : changeStage(s))} />
              {d.stageReason && <p className="mt-4 rounded-xl bg-paper px-4 py-2.5 text-sm text-muted">Причина последнего изменения: {d.stageReason}</p>}
            </div>
          )}
        </Card>

        {d && (
          <Card className="p-6">
            <p className="text-sm text-muted">Итоговая цена</p>
            <p className="display mt-1 text-4xl tabular">{money(d.finalPrice, d.currency)}</p>
            <div className="mt-4 divide-y divide-line/70">
              <KeyValue label="Цена квартиры" value={money(d.price, d.currency)} />
              <KeyValue label="Скидка" value={d.discount ? `${money(d.discount, d.currency)}` : '—'} />
              {d.discountReason && <KeyValue label="Причина" value={d.discountReason} />}
              {apartment.data && <KeyValue label="Квартира" value={`${roomsLabel(apartment.data.rooms)}, ${area(apartment.data.area)}`} />}
              <KeyValue label="Создана" value={dateShort(d.createdAt)} />
            </div>
          </Card>
        )}
      </motion.section>

      <Tabs
        className="mt-8"
        value={tab}
        onChange={setTab}
        tabs={[
          { value: 'schedule', label: <><CalendarRange className="size-4" /> График и платежи</> },
          { value: 'documents', label: <><FileText className="size-4" /> Документы</> },
          { value: 'chat', label: <><MessageCircle className="size-4" /> Чат с клиентом</> },
        ]}
      />
      <AnimatePresence mode="wait">
        <motion.div key={tab} initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }} transition={{ duration: 0.18 }} className="mt-6">
          {tab === 'schedule' && <ScheduleTab dealId={id} currency={d?.currency} />}
          {tab === 'documents' && <DocumentsTab dealId={id} />}
          {tab === 'chat' && <ChatPanel dealId={id} realm="staff" />}
        </motion.div>
      </AnimatePresence>

      <ConfirmDialog
        open={!!stageChange}
        onClose={() => setStageChange(null)}
        loading={busy}
        requireReason
        danger={stageChange === 'Cancelled'}
        title={stageChange === 'Cancelled' ? 'Отменить сделку?' : `Вернуть на этап «${stageChange ? dealStageLabel[stageChange] : ''}»?`}
        text={stageChange === 'Cancelled' ? 'Квартира вернётся в продажу. Это действие нельзя отменить.' : 'Статус квартиры изменится вместе с этапом.'}
        confirmLabel={stageChange === 'Cancelled' ? 'Отменить сделку' : 'Вернуть'}
        onConfirm={(reason) => stageChange && changeStage(stageChange, reason)}
      />
    </>
  );
}

function ScheduleTab({ dealId, currency }: { dealId: string; currency?: Currency }) {
  const staff = useStaff();
  const queryClient = useQueryClient();
  const toast = useToast();
  const q = useQuery({ queryKey: ['crm', 'schedule', dealId], queryFn: () => crm.schedule(dealId), retry: false });
  const [plan, setPlan] = useState(false);
  const [pay, setPay] = useState(false);
  const [reverse, setReverse] = useState<PaymentResponse | null>(null);
  const [busy, setBusy] = useState(false);
  const finance = staff.can(ROLES.finance);
  const noPlan = q.error instanceof ApiError && q.error.status === 404;

  if (q.isLoading) return <Skeleton className="h-96 rounded-2xl" />;
  if (q.error && !noPlan) return <ErrorState error={q.error} onRetry={() => q.refetch()} />;

  return (
    <>
      {noPlan ? (
        <EmptyState
          icon={<CalendarRange className="size-6" />}
          title="График платежей ещё не составлен"
          text="Составьте график: взнос, срок и день платежа. Пересоставить можно, пока нет оплат."
          action={staff.can(ROLES.sales) && <Button icon={<CalendarPlus className="size-4" />} onClick={() => setPlan(true)}>Составить график</Button>}
        />
      ) : (
        <>
          <div className="mb-4 flex flex-wrap justify-end gap-2">
            {staff.can(ROLES.sales) && q.data!.payments.length === 0 && (
              <Button variant="secondary" icon={<CalendarPlus className="size-4" />} onClick={() => setPlan(true)}>Пересоставить график</Button>
            )}
            {finance && q.data!.remaining > 0 && (
              <Button icon={<Wallet className="size-4" />} onClick={() => setPay(true)}>Внести платёж</Button>
            )}
          </div>
          <ScheduleView schedule={q.data!} realm="staff" onReverse={finance ? setReverse : undefined} />
        </>
      )}

      <PlanModal open={plan} onClose={() => setPlan(false)} dealId={dealId} />
      {q.data && <PaymentModal open={pay} onClose={() => setPay(false)} dealId={dealId} dealCurrency={q.data.currency ?? currency ?? 'TJS'} remaining={q.data.remaining} />}
      <ConfirmDialog
        open={!!reverse}
        onClose={() => setReverse(null)}
        loading={busy}
        danger
        requireReason
        title="Сторнировать платёж?"
        text={reverse && <>Будет создана обратная запись на {money(reverse.amount, reverse.currency, true)} от {dateShort(reverse.paidAt)}. Платёж не удаляется.</>}
        confirmLabel="Сторно"
        onConfirm={async (reason) => {
          if (!reverse) return;
          setBusy(true);
          try {
            await crm.reversePayment(reverse.id, reason);
            toast.success('Сторно проведено');
            setReverse(null);
            queryClient.invalidateQueries({ queryKey: ['crm'] });
          } catch (e) {
            toast.error(e);
          } finally {
            setBusy(false);
          }
        }}
      />
    </>
  );
}

function PlanModal({ open, onClose, dealId }: { open: boolean; onClose: () => void; dealId: string }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [down, setDown] = useState('30');
  const [months, setMonths] = useState('36');
  const [day, setDay] = useState('15');
  const [start, setStart] = useState(todayIso());
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(false);

  const submit = async () => {
    setLoading(true);
    setError(null);
    try {
      await crm.createPlan(dealId, { downPaymentPct: Number(down), months: Number(months), paymentDay: Number(day), startDate: start });
      toast.success('График составлен');
      queryClient.invalidateQueries({ queryKey: ['crm', 'schedule', dealId] });
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e : new ApiError(0, 'Ошибка'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="График платежей"
      subtitle="Месячный платёж округляется до целого, остаток уходит в последний платёж"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button loading={loading} onClick={submit}>Составить</Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Input label="Первоначальный взнос" type="number" min={0} max={100} value={down} onChange={(e) => setDown(e.target.value)} suffix="%" error={error?.field('downPaymentPct')} />
        <Input label="Срок" type="number" min={1} max={360} value={months} onChange={(e) => setMonths(e.target.value)} suffix="мес." error={error?.field('months')} />
        <Input label="День платежа" type="number" min={1} max={28} value={day} onChange={(e) => setDay(e.target.value)} hint="с 1 по 28 число" error={error?.field('paymentDay')} />
        <Input label="Дата начала" type="date" value={start} onChange={(e) => setStart(e.target.value)} error={error?.field('startDate')} />
        {error && !Object.keys(error.fieldErrors).length && <p className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger sm:col-span-2">{error.message}</p>}
      </div>
    </Modal>
  );
}

function PaymentModal({ open, onClose, dealId, dealCurrency, remaining }: { open: boolean; onClose: () => void; dealId: string; dealCurrency: Currency; remaining: number }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [amount, setAmount] = useState('');
  const [currency, setCurrency] = useState<Currency>(dealCurrency);
  const [paidAt, setPaidAt] = useState(todayIso());
  const [method, setMethod] = useState<PaymentMethod>('BankTransfer');
  const [comment, setComment] = useState('');
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(false);

  const submit = async () => {
    setLoading(true);
    setError(null);
    try {
      await crm.addPayment(dealId, { amount: Number(amount.replace(',', '.')), currency, paidAt, method, comment: comment.trim() || null });
      toast.success('Платёж внесён. Покупатель увидит его в кабинете.');
      queryClient.invalidateQueries({ queryKey: ['crm'] });
      setAmount('');
      setComment('');
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e : new ApiError(0, 'Ошибка'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Новый платёж"
      subtitle={`Остаток по договору: ${money(remaining, dealCurrency)}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button loading={loading} disabled={!Number(amount.replace(',', '.'))} onClick={submit} icon={<Plus className="size-4" />}>Внести</Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Input label="Сумма" inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} autoFocus error={error?.field('amount')} />
        <div className="flex flex-col gap-1.5">
          <span className="text-[13px] font-medium text-ink/80">Валюта платежа</span>
          <Segmented value={currency} onChange={setCurrency} options={[{ value: 'TJS', label: 'TJS' }, { value: 'USD', label: 'USD' }]} />
        </div>
        <Input label="Дата оплаты" type="date" max={todayIso()} value={paidAt} onChange={(e) => setPaidAt(e.target.value)} error={error?.field('paidAt')} />
        <Select label="Способ" value={method} onChange={(e) => setMethod(e.target.value as PaymentMethod)}>
          {(Object.keys(paymentMethodLabel) as PaymentMethod[]).map((m) => <option key={m} value={m}>{paymentMethodLabel[m]}</option>)}
        </Select>
        <Textarea className="sm:col-span-2" label="Комментарий" value={comment} onChange={(e) => setComment(e.target.value)} />
        {currency !== dealCurrency && <p className="rounded-xl bg-gold-soft px-4 py-3 text-sm sm:col-span-2">Сумма пересчитается в {dealCurrency} по курсу на дату оплаты.</p>}
        {error && !Object.keys(error.fieldErrors).length && <p className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger sm:col-span-2">{error.message}</p>}
      </div>
    </Modal>
  );
}

function DocumentsTab({ dealId }: { dealId: string }) {
  const q = useQuery({ queryKey: ['crm', 'documents', dealId], queryFn: () => crm.documents(dealId) });
  const [upload, setUpload] = useState(false);
  return (
    <>
      <div className="mb-4 flex justify-end">
        <Button icon={<Upload className="size-4" />} onClick={() => setUpload(true)}>Загрузить документ</Button>
      </div>
      {q.isLoading ? <Skeleton className="h-32" /> : q.error ? <ErrorState error={q.error} onRetry={() => q.refetch()} /> : <DocumentsList documents={q.data!} realm="staff" />}
      <UploadModal open={upload} onClose={() => setUpload(false)} dealId={dealId} />
    </>
  );
}

function UploadModal({ open, onClose, dealId }: { open: boolean; onClose: () => void; dealId: string }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [file, setFile] = useState<File | null>(null);
  const [type, setType] = useState<DocumentType>('Contract');
  const [signedAt, setSignedAt] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (!file) return;
    setLoading(true);
    setError(null);
    const form = new FormData();
    form.append('file', file);
    form.append('type', type);
    if (signedAt) form.append('signedAt', signedAt);
    try {
      await crm.uploadDocument(dealId, form);
      toast.success('Документ загружен и виден покупателю');
      queryClient.invalidateQueries({ queryKey: ['crm', 'documents', dealId] });
      setFile(null);
      setSignedAt('');
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
      title="Загрузка документа"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button loading={loading} disabled={!file} onClick={submit}>Загрузить</Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <FileDrop accept=".pdf,.png,.jpg,.jpeg,.docx" file={file} onFile={setFile} hint="PDF, PNG, JPG, DOCX до 20 МБ" />
        <div className="grid gap-4 sm:grid-cols-2">
          <Select label="Тип" value={type} onChange={(e) => setType(e.target.value as DocumentType)}>
            {(Object.keys(documentTypeLabel) as DocumentType[]).map((t) => <option key={t} value={t}>{documentTypeLabel[t]}</option>)}
          </Select>
          <Input label="Дата подписания" type="date" value={signedAt} onChange={(e) => setSignedAt(e.target.value)} />
        </div>
        {error && <p className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger">{error}</p>}
      </div>
    </Modal>
  );
}
