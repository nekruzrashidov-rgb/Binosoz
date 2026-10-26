import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { ChevronRight, Handshake, Plus } from 'lucide-react';
import { crm } from '@/api/endpoints';
import { DEAL_STAGES, type DealStage } from '@/api/types';
import { Badge, Button, Card, EmptyState, ErrorState, PageHeader, Pagination, Skeleton, Table, Tabs } from '@/components/ui';
import { dateShort, dealStageLabel, money } from '@/lib/format';
import { ROLES } from '../access';
import { DealFormModal } from '../forms';
import { Guard } from '../Guard';

export default function Deals() {
  return (
    <Guard roles={ROLES.sales}>
      <DealsContent />
    </Guard>
  );
}

function DealsContent() {
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const stage = (params.get('stage') as DealStage | null) ?? undefined;
  const [page, setPage] = useState(1);
  const q = useQuery({ queryKey: ['crm', 'deals', { stage, page }], queryFn: () => crm.deals({ stage, page }), placeholderData: (p) => p });
  const creating = params.get('new') === '1';

  const setParam = (key: string, value: string | null) => {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    setParams(next, { replace: true });
  };

  return (
    <>
      <PageHeader title="Сделки" subtitle={q.data ? `${q.data.totalCount} всего` : undefined} actions={<Button icon={<Plus className="size-4" />} onClick={() => setParam('new', '1')}>Новая сделка</Button>} />
      <Card>
        <Tabs
          className="px-3 pt-2"
          value={stage ?? 'all'}
          onChange={(v) => {
            setPage(1);
            setParam('stage', v === 'all' ? null : v);
          }}
          tabs={[{ value: 'all', label: 'Все' }, ...DEAL_STAGES.map((s) => ({ value: s, label: dealStageLabel[s] }))]}
        />
        {q.error ? (
          <ErrorState error={q.error} onRetry={() => q.refetch()} />
        ) : !q.data ? (
          <div className="flex flex-col gap-2 p-5">{Array.from({ length: 8 }).map((_, i) => <Skeleton key={i} className="h-12" />)}</div>
        ) : q.data.items.length === 0 ? (
          <EmptyState icon={<Handshake className="size-6" />} title="Сделок нет" />
        ) : (
          <>
            <Table head={['Квартира', 'Клиент', 'Этап', 'Цена', 'Скидка', 'Обновлена', '']}>
              {q.data.items.map((d) => (
                <tr key={d.id} className="group cursor-pointer" onClick={() => navigate(`/crm/deals/${d.id}`)}>
                  <td>
                    <span className="font-display text-2xl tabular">{d.apartmentNumber}</span>
                    <span className="ml-2 text-xs text-muted">{d.buildingName}</span>
                  </td>
                  <td className="font-medium">{d.customerName}</td>
                  <td><Badge tone={d.stage === 'Cancelled' ? 'red' : d.stage === 'Completed' ? 'green' : d.stage === 'Payment' ? 'dark' : 'gold'}>{dealStageLabel[d.stage]}</Badge></td>
                  <td className="font-semibold tabular">{money(d.finalPrice, d.currency)}</td>
                  <td className="text-muted tabular">{d.discount ? money(d.discount, d.currency) : '—'}</td>
                  <td className="text-muted tabular">{dateShort(d.updatedAt)}</td>
                  <td><ChevronRight className="ml-auto size-4 text-muted transition-transform group-hover:translate-x-1 group-hover:text-ink" /></td>
                </tr>
              ))}
            </Table>
            <Pagination page={page} pageSize={q.data.pageSize} total={q.data.totalCount} onChange={setPage} />
          </>
        )}
      </Card>
      <DealFormModal open={creating} onClose={() => setParam('new', null)} onCreated={(d) => navigate(`/crm/deals/${d.id}`)} />
    </>
  );
}
