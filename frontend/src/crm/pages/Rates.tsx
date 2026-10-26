import { useState } from 'react';
import { motion } from 'motion/react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Coins, Info } from 'lucide-react';
import { ApiError } from '@/api/client';
import { crm } from '@/api/endpoints';
import { Badge, Button, Card, CardHeader, Input, PageHeader, Skeleton, useToast } from '@/components/ui';
import { date, todayIso } from '@/lib/format';
import { ROLES } from '../access';
import { Guard } from '../Guard';

export default function Rates() {
  return (
    <Guard roles={ROLES.finance}>
      <Content />
    </Guard>
  );
}

function Content() {
  const queryClient = useQueryClient();
  const toast = useToast();
  const current = useQuery({ queryKey: ['crm', 'rate'], queryFn: crm.rate, retry: false });
  const [day, setDay] = useState(todayIso());
  const [value, setValue] = useState('');
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(false);
  const noRate = current.error instanceof ApiError && current.error.status === 404;

  const tomorrow = (() => {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  })();

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      await crm.setRate(day, Number(value.replace(',', '.')));
      toast.success('Курс сохранён');
      setValue('');
      queryClient.invalidateQueries({ queryKey: ['crm'] });
      queryClient.invalidateQueries({ queryKey: ['rate'] });
    } catch (err) {
      setError(err instanceof ApiError ? err : new ApiError(0, 'Ошибка'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <PageHeader title="Курс доллара" subtitle="Используется для сделок и платежей в USD и для оценочного эквивалента в графиках" />
      <div className="grid gap-6 lg:grid-cols-2">
        <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} className="relative overflow-hidden rounded-[24px] bg-ink p-8 text-white">
          <div className="absolute -right-16 -top-16 size-64 rounded-full bg-gold/20 blur-3xl" />
          <p className="flex items-center gap-2 text-sm text-white/55"><Coins className="size-4 text-gold" /> Текущий курс</p>
          {current.isLoading ? (
            <Skeleton className="mt-4 h-16 w-48 bg-white/10" />
          ) : noRate ? (
            <p className="display mt-4 text-4xl text-white/70">Курс не введён</p>
          ) : current.data ? (
            <>
              <p className="display mt-3 text-7xl tabular">
                {current.data.rateToTjs}
                <span className="ml-2 font-sans text-xl text-white/45">сом. за $1</span>
              </p>
              <div className="mt-6 flex items-center gap-3">
                <span className="text-sm text-white/55">на {date(current.data.date)}</span>
                <Badge className="bg-white/10 text-white/80">{current.data.source === 'Nbt' ? 'НБТ' : 'введён вручную'}</Badge>
              </div>
            </>
          ) : null}
        </motion.div>

        <Card>
          <CardHeader title="Ввести курс" subtitle="Не дальше чем на завтра. Ручной курс не перезаписывается загрузкой НБТ." />
          <form onSubmit={submit} className="flex flex-col gap-4 p-5">
            <div className="grid gap-4 sm:grid-cols-2">
              <Input label="Дата" type="date" max={tomorrow} value={day} onChange={(e) => setDay(e.target.value)} error={error?.field('date')} />
              <Input label="Курс" inputMode="decimal" placeholder="10.95" value={value} onChange={(e) => setValue(e.target.value)} suffix="сом." error={error?.field('rateToTjs')} />
            </div>
            {error && !Object.keys(error.fieldErrors).length && <p className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger">{error.message}</p>}
            <Button type="submit" loading={loading} disabled={!Number(value.replace(',', '.'))}>Сохранить курс</Button>
            <p className="flex items-start gap-2 text-xs text-muted">
              <Info className="mt-0.5 size-3.5 shrink-0" /> Платёж в другой валюте пересчитывается по курсу на дату оплаты или ближайший более ранний.
            </p>
          </form>
        </Card>
      </div>
    </>
  );
}
