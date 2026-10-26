import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { motion } from 'motion/react';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, ChevronRight, Eye, EyeOff, Handshake, Mail, Phone, Plus, ShieldCheck } from 'lucide-react';
import { crm } from '@/api/endpoints';
import { Badge, Button, Card, CardHeader, EmptyState, ErrorState, KeyValue, Skeleton } from '@/components/ui';
import { dateShort, dealStageLabel, formatPhone, money } from '@/lib/format';
import { ROLES } from '../access';
import { DealFormModal } from '../forms';
import { Guard } from '../Guard';

export default function CustomerDetail() {
  return (
    <Guard roles={ROLES.sales}>
      <Content />
    </Guard>
  );
}

function Content() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const customer = useQuery({ queryKey: ['crm', 'customer', id], queryFn: () => crm.customer(id) });
  const deals = useQuery({ queryKey: ['crm', 'deals', { customerId: id }], queryFn: () => crm.deals({ customerId: id }) });
  const [showPassport, setShowPassport] = useState(false);
  const [newDeal, setNewDeal] = useState(false);
  const c = customer.data;

  if (customer.error) return <ErrorState error={customer.error} onRetry={() => customer.refetch()} />;

  return (
    <>
      <Link to="/crm/customers" className="inline-flex items-center gap-1.5 text-sm text-muted hover:text-ink">
        <ArrowLeft className="size-4" /> Клиенты
      </Link>
      <div className="mt-4 grid gap-6 lg:grid-cols-[360px_1fr]">
        <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }}>
          <Card className="overflow-hidden">
            <div className="bg-ink p-6 text-white">
              <span className="flex size-14 items-center justify-center rounded-2xl bg-gold text-lg font-semibold text-ink">
                {c ? c.fullName.split(' ').map((p) => p[0]).slice(0, 2).join('') : ''}
              </span>
              <h1 className="mt-4 text-2xl font-semibold">{c?.fullName ?? <Skeleton className="h-7 w-48 bg-white/10" />}</h1>
              {c && <p className="mt-1 text-sm text-white/50">Клиент с {dateShort(c.createdAt)}</p>}
            </div>
            <div className="divide-y divide-line/70 px-6 py-2">
              <KeyValue label={<span className="flex items-center gap-2"><Phone className="size-3.5" /> Телефон</span>} value={c ? <a href={`tel:${c.phone}`} className="hover:text-gold-2">{formatPhone(c.phone)}</a> : '…'} />
              <KeyValue label={<span className="flex items-center gap-2"><Mail className="size-3.5" /> Почта</span>} value={c?.email ?? '—'} />
              <KeyValue label="Кабинет" value={c?.userId ? <Badge tone="green">подключён</Badge> : <Badge>не входил</Badge>} />
            </div>
            <div className="border-t border-line/70 p-6">
              <div className="flex items-center justify-between">
                <p className="flex items-center gap-2 text-sm font-medium">
                  <ShieldCheck className="size-4 text-free" /> Паспортные данные
                </p>
                <button onClick={() => setShowPassport((v) => !v)} className="flex items-center gap-1 text-xs text-muted hover:text-ink">
                  {showPassport ? <EyeOff className="size-3.5" /> : <Eye className="size-3.5" />} {showPassport ? 'Скрыть' : 'Показать'}
                </button>
              </div>
              <p className="mt-2 whitespace-pre-wrap rounded-xl bg-paper px-4 py-3 text-sm">
                {!c ? '…' : !c.passportData ? <span className="text-muted">Не указаны</span> : showPassport ? c.passportData : '•••• •••••• ••••'}
              </p>
            </div>
          </Card>
        </motion.div>

        <Card>
          <CardHeader
            title="Сделки клиента"
            action={c && <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setNewDeal(true)}>Новая сделка</Button>}
          />
          {deals.isLoading ? (
            <div className="flex flex-col gap-2 p-5"><Skeleton className="h-16" /><Skeleton className="h-16" /></div>
          ) : deals.error ? (
            <ErrorState error={deals.error} onRetry={() => deals.refetch()} />
          ) : deals.data!.items.length === 0 ? (
            <EmptyState icon={<Handshake className="size-6" />} title="Сделок пока нет" />
          ) : (
            <div className="p-2">
              {deals.data!.items.map((d) => (
                <Link key={d.id} to={`/crm/deals/${d.id}`} className="group flex items-center gap-4 rounded-xl px-4 py-3.5 transition-colors hover:bg-paper">
                  <span className="font-display text-3xl tabular">{d.apartmentNumber}</span>
                  <span className="flex-1">
                    <span className="block text-sm font-medium">{d.buildingName}</span>
                    <span className="block text-xs text-muted">с {dateShort(d.createdAt)}</span>
                  </span>
                  <Badge tone={d.stage === 'Cancelled' ? 'red' : d.stage === 'Completed' ? 'green' : 'gold'}>{dealStageLabel[d.stage]}</Badge>
                  <span className="w-32 text-right text-sm font-semibold tabular">{money(d.finalPrice, d.currency)}</span>
                  <ChevronRight className="size-4 text-muted transition-transform group-hover:translate-x-1" />
                </Link>
              ))}
            </div>
          )}
        </Card>
      </div>
      <DealFormModal open={newDeal} onClose={() => setNewDeal(false)} customer={c ?? null} onCreated={(d) => navigate(`/crm/deals/${d.id}`)} />
    </>
  );
}
