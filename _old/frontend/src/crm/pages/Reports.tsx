import { useState } from 'react';
import { Link } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, FileSpreadsheet, TrendingUp } from 'lucide-react';
import { downloadAuthorized } from '@/api/client';
import { crm } from '@/api/endpoints';
import { Badge, Button, Card, EmptyState, ErrorState, Input, PageHeader, Skeleton, StatCard, Table, Tabs, useToast } from '@/components/ui';
import { cn } from '@/lib/cn';
import { dateShort, formatPhone, money, monthStartIso, paymentMethodLabel, todayIso } from '@/lib/format';
import { ROLES } from '../access';
import { Guard } from '../Guard';

export default function Reports() {
  return (
    <Guard roles={ROLES.finance}>
      <Content />
    </Guard>
  );
}

function Content() {
  const [tab, setTab] = useState<'payments' | 'debts'>('payments');
  return (
    <>
      <PageHeader title="Отчёты" subtitle="Поступления и задолженность. Каждый отчёт можно выгрузить в Excel." />
      <Tabs value={tab} onChange={setTab} tabs={[{ value: 'payments', label: 'Поступления' }, { value: 'debts', label: 'Задолженность' }]} />
      <div className="mt-6">{tab === 'payments' ? <PaymentsReport /> : <DebtsReport />}</div>
    </>
  );
}

function ExcelButton({ path, query, name }: { path: string; query: Record<string, string>; name: string }) {
  const toast = useToast();
  const [busy, setBusy] = useState(false);
  return (
    <Button
      variant="secondary"
      loading={busy}
      icon={<FileSpreadsheet className="size-4 text-free" />}
      onClick={async () => {
        setBusy(true);
        try {
          await downloadAuthorized(path, 'staff', query, name);
        } catch (e) {
          toast.error(e);
        } finally {
          setBusy(false);
        }
      }}
    >
      Excel
    </Button>
  );
}

function PaymentsReport() {
  const [from, setFrom] = useState(monthStartIso());
  const [to, setTo] = useState(todayIso());
  const q = useQuery({ queryKey: ['crm', 'report', 'payments', from, to], queryFn: () => crm.paymentsReport(from, to), placeholderData: (p) => p });
  const r = q.data;

  return (
    <>
      <div className="mb-5 flex flex-wrap items-end gap-3">
        <Input className="w-44" type="date" label="С" value={from} max={to} onChange={(e) => setFrom(e.target.value)} />
        <Input className="w-44" type="date" label="По" value={to} min={from} max={todayIso()} onChange={(e) => setTo(e.target.value)} />
        <div className="ml-auto">
          <ExcelButton path="/api/v1/reports/payments.xlsx" query={{ from, to }} name={`Поступления ${from} — ${to}.xlsx`} />
        </div>
      </div>
      {q.error ? (
        <ErrorState error={q.error} onRetry={() => q.refetch()} />
      ) : !r ? (
        <Skeleton className="h-80 rounded-2xl" />
      ) : (
        <>
          <div className="mb-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <StatCard tone="dark" label="Итого в сомони" value={money(r.totalTjs)} icon={<TrendingUp className="size-4" />} />
            {r.totalsByCurrency.map((t, i) => (
              <StatCard key={t.currency} label={`В ${t.currency}`} value={money(t.amount, t.currency)} delay={0.05 * (i + 1)} />
            ))}
            <StatCard label="Операций" value={r.rows.length} hint={`${r.rows.filter((x) => x.isReversal).length} сторно`} delay={0.15} />
          </div>
          <Card>
            {r.rows.length === 0 ? (
              <EmptyState title="За период поступлений нет" />
            ) : (
              <Table head={['Дата', 'Клиент', 'Квартира', 'Сумма', 'Курс', 'В сомони', 'Способ', '']}>
                {r.rows.map((row) => (
                  <tr key={row.paymentId} className={cn(row.isReversal && 'bg-danger-soft/40')}>
                    <td className="tabular">{dateShort(row.paidAt)}</td>
                    <td className="font-medium">
                      <Link to={`/crm/deals/${row.dealId}`} className="hover:text-gold-2">{row.customerName}</Link>
                    </td>
                    <td className="text-muted">№{row.apartmentNumber}</td>
                    <td className={cn('font-semibold tabular', row.amount < 0 && 'text-danger')}>{money(row.amount, row.currency, true)}</td>
                    <td className="text-muted tabular">{row.rateUsed ?? '—'}</td>
                    <td className="tabular">{money(row.amountTjs, 'TJS', true)}</td>
                    <td>{paymentMethodLabel[row.method]}</td>
                    <td>{row.isReversal && <Badge tone="red">Сторно</Badge>}</td>
                  </tr>
                ))}
              </Table>
            )}
          </Card>
        </>
      )}
    </>
  );
}

function DebtsReport() {
  const [asOf, setAsOf] = useState(todayIso());
  const q = useQuery({ queryKey: ['crm', 'report', 'debts', asOf], queryFn: () => crm.debtsReport(asOf), placeholderData: (p) => p });
  const r = q.data;

  return (
    <>
      <div className="mb-5 flex flex-wrap items-end gap-3">
        <Input className="w-44" type="date" label="На дату" value={asOf} max={todayIso()} onChange={(e) => setAsOf(e.target.value)} />
        <div className="ml-auto">
          <ExcelButton path="/api/v1/reports/debts.xlsx" query={{ asOf }} name={`Задолженность на ${asOf}.xlsx`} />
        </div>
      </div>
      {q.error ? (
        <ErrorState error={q.error} onRetry={() => q.refetch()} />
      ) : !r ? (
        <Skeleton className="h-80 rounded-2xl" />
      ) : (
        <>
          <div className="mb-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <StatCard tone={r.rows.length ? 'gold' : 'default'} label="Должников" value={r.rows.length} icon={<AlertTriangle className="size-4" />} />
            {r.totalsByCurrency.map((t, i) => (
              <StatCard key={t.currency} label={`Просрочено, ${t.currency}`} value={money(t.amount, t.currency)} delay={0.05 * (i + 1)} />
            ))}
          </div>
          <Card>
            {r.rows.length === 0 ? (
              <EmptyState title="Просрочек нет" text="Все платежи по графикам внесены вовремя." />
            ) : (
              <Table head={['Клиент', 'Квартира', 'Просрочено', 'С даты', 'Дней', 'Остаток по договору']}>
                {r.rows.map((row) => (
                  <tr key={row.dealId}>
                    <td>
                      <Link to={`/crm/deals/${row.dealId}`} className="font-medium hover:text-gold-2">{row.customerName}</Link>
                      <a href={`tel:${row.phone}`} className="block text-xs text-muted tabular hover:text-ink">{formatPhone(row.phone)}</a>
                    </td>
                    <td className="text-muted">№{row.apartmentNumber}</td>
                    <td className="font-semibold text-danger tabular">{money(row.overdue, row.currency, true)}</td>
                    <td className="tabular">{dateShort(row.oldestOverdueDate)}</td>
                    <td>
                      <Badge tone={row.daysOverdue > 30 ? 'red' : 'amber'}>{row.daysOverdue}</Badge>
                    </td>
                    <td className="tabular">{money(row.remaining, row.currency, true)}</td>
                  </tr>
                ))}
              </Table>
            )}
          </Card>
        </>
      )}
    </>
  );
}
