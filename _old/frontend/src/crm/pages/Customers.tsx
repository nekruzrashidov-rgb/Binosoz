import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { ChevronRight, Plus, Search, Users } from 'lucide-react';
import { crm } from '@/api/endpoints';
import { Button, Card, EmptyState, ErrorState, PageHeader, Pagination, Skeleton, Table } from '@/components/ui';
import { dateShort, formatPhone } from '@/lib/format';
import { ROLES } from '../access';
import { CustomerFormModal } from '../forms';
import { Guard } from '../Guard';

export default function Customers() {
  return (
    <Guard roles={ROLES.sales}>
      <CustomersContent />
    </Guard>
  );
}

function CustomersContent() {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [debounced, setDebounced] = useState('');
  const [page, setPage] = useState(1);
  const [create, setCreate] = useState(false);

  useEffect(() => {
    const t = setTimeout(() => {
      setDebounced(search.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(t);
  }, [search]);

  const q = useQuery({ queryKey: ['crm', 'customers', debounced, page], queryFn: () => crm.customers(debounced || undefined, page), placeholderData: (prev) => prev });

  return (
    <>
      <PageHeader title="Клиенты" subtitle={q.data ? `${q.data.totalCount} в базе` : undefined} actions={<Button icon={<Plus className="size-4" />} onClick={() => setCreate(true)}>Новый клиент</Button>} />
      <Card>
        <div className="border-b border-line/70 p-4">
          <div className="relative max-w-md">
            <Search className="absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-muted" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Поиск по имени или телефону"
              className="h-11 w-full rounded-xl border border-line bg-paper/50 pl-10 pr-4 text-sm outline-none transition focus:border-gold focus:bg-white focus:ring-4 focus:ring-gold/15"
            />
          </div>
        </div>
        {q.error ? (
          <ErrorState error={q.error} onRetry={() => q.refetch()} />
        ) : !q.data ? (
          <div className="flex flex-col gap-2 p-5">{Array.from({ length: 8 }).map((_, i) => <Skeleton key={i} className="h-12" />)}</div>
        ) : q.data.items.length === 0 ? (
          <EmptyState icon={<Users className="size-6" />} title={debounced ? 'Никого не нашли' : 'Клиентов пока нет'} />
        ) : (
          <>
            <Table head={['Клиент', 'Телефон', 'Почта', 'В базе с', '']}>
              {q.data.items.map((c) => (
                <tr key={c.id} onClick={() => navigate(`/crm/customers/${c.id}`)} className="group cursor-pointer">
                  <td>
                    <span className="flex items-center gap-3">
                      <span className="flex size-9 items-center justify-center rounded-full bg-paper-2 text-xs font-semibold transition-colors group-hover:bg-ink group-hover:text-gold">
                        {c.fullName.split(' ').map((p) => p[0]).slice(0, 2).join('')}
                      </span>
                      <span className="font-medium">{c.fullName}</span>
                    </span>
                  </td>
                  <td className="tabular">{formatPhone(c.phone)}</td>
                  <td className="text-muted">{c.email ?? '—'}</td>
                  <td className="text-muted tabular">{dateShort(c.createdAt)}</td>
                  <td className="text-right">
                    <ChevronRight className="ml-auto size-4 text-muted transition-transform group-hover:translate-x-1 group-hover:text-ink" />
                  </td>
                </tr>
              ))}
            </Table>
            <Pagination page={page} pageSize={q.data.pageSize} total={q.data.totalCount} onChange={setPage} />
          </>
        )}
      </Card>
      <CustomerFormModal open={create} onClose={() => setCreate(false)} onCreated={(c) => navigate(`/crm/customers/${c.id}`)} />
    </>
  );
}
