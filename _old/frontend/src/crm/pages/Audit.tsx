import { Fragment, useState } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { useQuery } from '@tanstack/react-query';
import { ChevronDown, ScrollText } from 'lucide-react';
import { crm } from '@/api/endpoints';
import type { AuditLog } from '@/api/types';
import { Badge, Card, EmptyState, ErrorState, Input, PageHeader, Pagination, Select, Skeleton, Table } from '@/components/ui';
import { cn } from '@/lib/cn';
import { auditActionLabel, dateTime } from '@/lib/format';
import { ROLES } from '../access';
import { Guard } from '../Guard';

// Имена сущностей — как их пишет сервер (nameof класса).
const ENTITIES: Record<string, string> = {
  Apartment: 'Квартира',
  Building: 'Дом',
  Booking: 'Бронь',
  Deal: 'Сделка',
  PaymentPlan: 'График',
  Payment: 'Платёж',
  Document: 'Документ',
  ExchangeRate: 'Курс',
  ConstructionStage: 'Этап стройки',
  ConstructionPhoto: 'Фото стройки',
  AppUser: 'Сотрудник',
  Lead: 'Заявка',
  Customer: 'Клиент',
};

export default function Audit() {
  return (
    <Guard roles={ROLES.admin}>
      <Content />
    </Guard>
  );
}

function Content() {
  const [entity, setEntity] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [page, setPage] = useState(1);
  const [open, setOpen] = useState<string | null>(null);
  const q = useQuery({
    queryKey: ['crm', 'audit', entity, from, to, page],
    queryFn: () => crm.audit({ entity: entity || undefined, from: from || undefined, to: to ? `${to}T23:59:59` : undefined, page }),
    placeholderData: (p) => p,
  });

  return (
    <>
      <PageHeader title="Журнал изменений" subtitle="Статусы, цены, платежи, документы, курс и пользователи: кто, когда, что было и что стало" />
      <Card>
        <div className="flex flex-wrap items-end gap-3 border-b border-line/70 p-4">
          <Select className="w-48" label="Объект" value={entity} onChange={(e) => { setEntity(e.target.value); setPage(1); }}>
            <option value="">Все</option>
            {Object.entries(ENTITIES).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </Select>
          <Input className="w-44" type="date" label="С" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1); }} />
          <Input className="w-44" type="date" label="По" value={to} onChange={(e) => { setTo(e.target.value); setPage(1); }} />
        </div>
        {q.error ? (
          <ErrorState error={q.error} onRetry={() => q.refetch()} />
        ) : !q.data ? (
          <div className="flex flex-col gap-2 p-5">{Array.from({ length: 10 }).map((_, i) => <Skeleton key={i} className="h-11" />)}</div>
        ) : q.data.items.length === 0 ? (
          <EmptyState icon={<ScrollText className="size-6" />} title="Записей нет" />
        ) : (
          <>
            <Table head={['Когда', 'Кто', 'Объект', 'Действие', '']}>
              {q.data.items.map((log) => (
                <Fragment key={log.id}>
                  <tr className="cursor-pointer" onClick={() => setOpen((o) => (o === log.id ? null : log.id))}>
                    <td className="whitespace-nowrap tabular text-muted">{dateTime(log.at)}</td>
                    <td>{log.userEmail ?? <span className="text-muted">система</span>}</td>
                    <td><Badge>{ENTITIES[log.entity] ?? log.entity}</Badge></td>
                    <td className="font-medium">{auditActionLabel[log.action] ?? log.action}</td>
                    <td><ChevronDown className={cn('ml-auto size-4 text-muted transition-transform', open === log.id && 'rotate-180')} /></td>
                  </tr>
                  <AnimatePresence initial={false}>
                    {open === log.id && (
                      <tr className="!bg-paper/60 hover:!bg-paper/60">
                        <td colSpan={5} className="!py-0">
                          <motion.div initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} className="overflow-hidden">
                            <Diff log={log} />
                          </motion.div>
                        </td>
                      </tr>
                    )}
                  </AnimatePresence>
                </Fragment>
              ))}
            </Table>
            <Pagination page={page} pageSize={q.data.pageSize} total={q.data.totalCount} onChange={setPage} />
          </>
        )}
      </Card>
    </>
  );
}

// Было / стало: разбираем JSON и показываем только изменившиеся поля.
function Diff({ log }: { log: AuditLog }) {
  const parse = (v: string | null): Record<string, unknown> => {
    if (!v) return {};
    try {
      const o = JSON.parse(v);
      return typeof o === 'object' && o ? (o as Record<string, unknown>) : { значение: o };
    } catch {
      return { значение: v };
    }
  };
  const before = parse(log.before);
  const after = parse(log.after);
  const keys = [...new Set([...Object.keys(before), ...Object.keys(after)])];
  const show = (v: unknown) => (v === undefined ? '—' : typeof v === 'object' ? JSON.stringify(v) : String(v));
  return (
    <div className="py-4">
      <p className="mb-2 text-xs text-muted">ID объекта: <span className="font-mono">{log.entityId}</span></p>
      <div className="overflow-hidden rounded-xl border border-line/70 bg-white">
        <div className="grid grid-cols-[180px_1fr_1fr] border-b border-line/70 bg-paper/60 px-4 py-2 text-[11px] font-semibold uppercase tracking-wider text-muted">
          <span>Поле</span><span>Было</span><span>Стало</span>
        </div>
        {keys.map((k) => {
          const changed = show(before[k]) !== show(after[k]);
          return (
            <div key={k} className="grid grid-cols-[180px_1fr_1fr] gap-2 border-b border-line/50 px-4 py-2 text-sm last:border-0">
              <span className="text-muted">{k}</span>
              <span className={cn('break-all', changed && 'text-danger line-through decoration-danger/40')}>{show(before[k])}</span>
              <span className={cn('break-all', changed && 'font-medium text-free')}>{show(after[k])}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}
