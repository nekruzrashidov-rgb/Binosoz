import { useState } from 'react';
import { motion } from 'motion/react';
import { AlertTriangle, CalendarDays, FileDown, FileText, Info, Undo2 } from 'lucide-react';
import { downloadAuthorized } from '@/api/client';
import type { Realm } from '@/api/session';
import type { PaymentResponse, ScheduleResponse } from '@/api/types';
import { Badge, Button, Card, CardHeader, Input, Modal, ProgressBar, Table, useToast } from '@/components/ui';
import { cn } from '@/lib/cn';
import { dateShort, money, paymentMethodLabel, scheduleStatusLabel, todayIso } from '@/lib/format';

// График платежей и история оплат — одинаково в кабинете и в CRM.
// onReverse передаёт только CRM (сторно доступно бухгалтеру и админу).
export function ScheduleView({
  schedule,
  realm,
  onReverse,
}: {
  schedule: ScheduleResponse;
  realm: Realm;
  onReverse?: (payment: PaymentResponse) => void;
}) {
  const s = schedule;
  const paidPct = s.total ? Math.round((s.paid / s.total) * 100) : 0;
  const next = s.items.find((i) => i.status !== 'Paid');
  const eq = s.equivalent;
  const toast = useToast();
  const [statementOpen, setStatementOpen] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);

  const download = async (kind: 'schedule' | 'statement', asOf?: string) => {
    setBusy(kind);
    try {
      if (kind === 'schedule') await downloadAuthorized(`/api/v1/deals/${s.dealId}/schedule.pdf`, realm, undefined, 'График платежей.pdf');
      else await downloadAuthorized(`/api/v1/deals/${s.dealId}/statement.pdf`, realm, { date: asOf }, 'Акт сверки.pdf');
      setStatementOpen(false);
    } catch (e) {
      toast.error(e);
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <div className="grid gap-4 md:grid-cols-[1.3fr_1fr]">
        <Card className="overflow-hidden">
          <div className="bg-ink p-6 text-white">
            <div className="flex items-start justify-between">
              <div>
                <p className="text-sm text-white/55">Оплачено</p>
                <p className="display mt-1 text-5xl tabular">{money(s.paid, s.currency)}</p>
                <p className="mt-1 text-sm text-white/45 tabular">из {money(s.total, s.currency)}</p>
              </div>
              <span className="display text-4xl text-gold tabular">{paidPct}%</span>
            </div>
            <ProgressBar value={paidPct} className="mt-5 bg-white/10" />
            <div className="mt-6 grid grid-cols-2 gap-4 text-sm">
              <div>
                <p className="text-white/45">Остаток</p>
                <p className="mt-0.5 font-semibold tabular">{money(s.remaining, s.currency)}</p>
              </div>
              <div>
                <p className="text-white/45">Просрочено</p>
                <p className={cn('mt-0.5 font-semibold tabular', s.overdue > 0 && 'text-[#ff9b92]')}>{money(s.overdue, s.currency)}</p>
              </div>
            </div>
          </div>
          {eq && (
            <div className="flex items-start gap-3 bg-gold-soft/60 px-6 py-3.5 text-sm">
              <Info className="mt-0.5 size-4 shrink-0 text-gold-2" />
              <p className="text-ink/75">
                ≈ {money(eq.total, eq.currency)} всего, остаток ≈ {money(eq.remaining, eq.currency)}. <span className="text-muted">{eq.note}</span>
              </p>
            </div>
          )}
        </Card>

        <Card className="flex flex-col p-6">
          <p className="flex items-center gap-2 text-sm text-muted">
            <CalendarDays className="size-4" /> Следующий платёж
          </p>
          {next ? (
            <>
              <p className="display mt-3 text-4xl tabular">{money(next.amountDue - next.amountPaid, s.currency)}</p>
              <p className={cn('mt-1 text-sm', next.isOverdue ? 'font-medium text-danger' : 'text-muted')}>
                {next.isOverdue ? 'Просрочен с ' : 'до '}
                {dateShort(next.dueDate)}
              </p>
            </>
          ) : (
            <p className="display mt-3 text-3xl text-free">Всё оплачено</p>
          )}
          <div className="mt-auto flex flex-col gap-2 pt-6">
            <Button variant="secondary" icon={<FileDown className="size-4" />} loading={busy === 'schedule'} onClick={() => download('schedule')}>
              График в PDF
            </Button>
            <Button variant="secondary" icon={<FileText className="size-4" />} onClick={() => setStatementOpen(true)}>
              Акт сверки
            </Button>
          </div>
        </Card>
      </div>

      <Card>
        <CardHeader
          title="График платежей"
          subtitle={`Взнос ${s.downPaymentPct}% · ${s.months} мес. · платёж до ${s.paymentDay} числа`}
        />
        <Table head={['Дата', 'Платёж', 'К оплате', 'Оплачено', eq ? `≈ ${eq.currency}` : null, 'Статус'].filter(Boolean)}>
          {s.items.map((item, i) => (
            <motion.tr key={item.id} initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ delay: Math.min(i, 20) * 0.015 }}>
              <td className="tabular">{dateShort(item.dueDate)}</td>
              <td>
                {item.kind === 'DownPayment'
                  ? 'Первоначальный взнос'
                  : `Платёж ${s.items.slice(0, i + 1).filter((x) => x.kind === 'Monthly').length}`}
              </td>
              <td className="font-medium tabular">{money(item.amountDue, s.currency)}</td>
              <td className="tabular text-muted">{money(item.amountPaid, s.currency)}</td>
              {eq && <td className="tabular text-muted">{money(item.amountDueEquivalent, eq.currency)}</td>}
              <td>
                {item.isOverdue ? (
                  <Badge tone="red">
                    <AlertTriangle className="size-3" /> Просрочен
                  </Badge>
                ) : (
                  <Badge tone={item.status === 'Paid' ? 'green' : item.status === 'PartiallyPaid' ? 'amber' : 'neutral'}>{scheduleStatusLabel[item.status]}</Badge>
                )}
              </td>
            </motion.tr>
          ))}
        </Table>
      </Card>

      <Card>
        <CardHeader title="Платежи" subtitle={s.payments.length ? `${s.payments.length} операций` : 'Оплат пока не было'} />
        {s.payments.length > 0 && (
          <Table head={['Дата', 'Сумма', 'Курс', `В ${s.currency}`, 'Способ', 'Комментарий', onReverse ? '' : null].filter((h) => h !== null)}>
            {[...s.payments]
              .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
              .map((p) => {
                const reversal = !!p.reversalOfId;
                return (
                  <tr key={p.id} className={cn(reversal && 'bg-danger-soft/40', p.isReversed && 'text-muted')}>
                    <td className="tabular">{dateShort(p.paidAt)}</td>
                    <td className={cn('font-medium tabular', p.isReversed && 'line-through')}>{money(p.amount, p.currency, true)}</td>
                    <td className="tabular text-muted">{p.rateUsed ?? '—'}</td>
                    <td className="tabular">{money(p.amountInDealCurrency, s.currency, true)}</td>
                    <td>{paymentMethodLabel[p.method]}</td>
                    <td className="max-w-[220px] truncate text-muted">
                      {reversal ? <Badge tone="red">Сторно</Badge> : p.isReversed ? <Badge>Отменён</Badge> : null} {p.comment}
                    </td>
                    {onReverse && (
                      <td className="text-right">
                        {!reversal && !p.isReversed && (
                          <button onClick={() => onReverse(p)} className="inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs text-muted transition hover:bg-danger-soft hover:text-danger">
                            <Undo2 className="size-3.5" /> Сторно
                          </button>
                        )}
                      </td>
                    )}
                  </tr>
                );
              })}
          </Table>
        )}
      </Card>

      <StatementModal open={statementOpen} onClose={() => setStatementOpen(false)} loading={busy === 'statement'} onDownload={(d) => download('statement', d)} />
    </div>
  );
}

function StatementModal({ open, onClose, onDownload, loading }: { open: boolean; onClose: () => void; onDownload: (date: string) => void; loading: boolean }) {
  const [value, setValue] = useState(todayIso());
  return (
    <Modal
      open={open}
      onClose={onClose}
      size="sm"
      title="Акт сверки"
      subtitle="Начислено, оплачено и задолженность на выбранную дату"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button loading={loading} icon={<FileDown className="size-4" />} onClick={() => onDownload(value)}>
            Скачать PDF
          </Button>
        </>
      }
    >
      <Input type="date" label="На дату" value={value} max={todayIso()} onChange={(e) => setValue(e.target.value)} />
    </Modal>
  );
}
