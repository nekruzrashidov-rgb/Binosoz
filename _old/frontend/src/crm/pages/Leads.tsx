import { useState } from 'react';
import { Link } from 'react-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Ban, Hand, MessageSquarePlus, Phone, Plus, UserPlus } from 'lucide-react';
import { crm } from '@/api/endpoints';
import type { LeadResponse, LeadSource, LeadStatus } from '@/api/types';
import { Badge, Button, Card, ConfirmDialog, EmptyState, ErrorState, Input, Modal, PageHeader, Pagination, PhoneInput, Select, Skeleton, Table, Tabs, Textarea, useToast } from '@/components/ui';
import { dateTime, formatPhone, leadSourceLabel, leadStatusLabel, normalizePhone } from '@/lib/format';
import { ROLES, useStaff } from '../access';
import { CustomerFormModal } from '../forms';
import { Guard } from '../Guard';

const TONE: Record<LeadStatus, 'blue' | 'amber' | 'green' | 'red'> = { New: 'blue', InProgress: 'amber', Converted: 'green', Rejected: 'red' };

export default function Leads() {
  return (
    <Guard roles={ROLES.sales}>
      <LeadsContent />
    </Guard>
  );
}

function LeadsContent() {
  const queryClient = useQueryClient();
  const toast = useToast();
  const { user } = useStaff();
  const [status, setStatus] = useState<LeadStatus | 'all'>('New');
  const [page, setPage] = useState(1);
  const q = useQuery({ queryKey: ['crm', 'leads', status, page], queryFn: () => crm.leads(status === 'all' ? undefined : status, page) });
  const [convert, setConvert] = useState<LeadResponse | null>(null);
  const [reject, setReject] = useState<LeadResponse | null>(null);
  const [comment, setComment] = useState<LeadResponse | null>(null);
  const [create, setCreate] = useState(false);

  const update = async (lead: LeadResponse, body: Parameters<typeof crm.updateLead>[1], message: string) => {
    try {
      await crm.updateLead(lead.id, body);
      toast.success(message);
      queryClient.invalidateQueries({ queryKey: ['crm'] });
    } catch (e) {
      toast.error(e);
    }
  };

  return (
    <>
      <PageHeader
        title="Заявки"
        subtitle="Обращения с сайта, звонки и визиты в офис"
        actions={<Button icon={<Plus className="size-4" />} onClick={() => setCreate(true)}>Добавить заявку</Button>}
      />
      <Card>
        <Tabs
          className="px-3 pt-2"
          value={status}
          onChange={(v) => {
            setStatus(v);
            setPage(1);
          }}
          tabs={[
            { value: 'New', label: 'Новые' },
            { value: 'InProgress', label: 'В работе' },
            { value: 'Converted', label: 'Клиенты' },
            { value: 'Rejected', label: 'Отказ' },
            { value: 'all', label: 'Все' },
          ]}
        />
        {q.error ? (
          <ErrorState error={q.error} onRetry={() => q.refetch()} />
        ) : q.isLoading ? (
          <div className="flex flex-col gap-2 p-5">{Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-12" />)}</div>
        ) : q.data!.items.length === 0 ? (
          <EmptyState title="Заявок нет" text={status === 'New' ? 'Все новые заявки разобраны.' : undefined} />
        ) : (
          <>
            <Table head={['Клиент', 'Источник', 'Квартира', 'Комментарий', 'Статус', 'Создана', '']}>
              {q.data!.items.map((l) => (
                <tr key={l.id}>
                  <td>
                    <p className="font-medium">{l.name}</p>
                    <a href={`tel:${l.phone}`} className="flex items-center gap-1 text-xs text-muted tabular hover:text-ink">
                      <Phone className="size-3" /> {formatPhone(l.phone)}
                    </a>
                  </td>
                  <td>{leadSourceLabel[l.source]}</td>
                  <td>{l.apartmentId ? <Link to={`/apartments/${l.apartmentId}`} target="_blank" className="text-gold-2 hover:text-ink">открыть</Link> : <span className="text-muted">—</span>}</td>
                  <td className="max-w-[240px] truncate text-muted" title={l.comment ?? ''}>{l.comment ?? '—'}</td>
                  <td>
                    <Badge tone={TONE[l.status]}>{leadStatusLabel[l.status]}</Badge>
                    {l.managerId === user?.id && <span className="ml-1.5 text-[11px] text-muted">моя</span>}
                  </td>
                  <td className="whitespace-nowrap text-muted tabular">{dateTime(l.createdAt)}</td>
                  <td>
                    <div className="flex justify-end gap-1">
                      {l.status === 'New' && (
                        <IconAction title="Взять в работу" onClick={() => update(l, { status: 'InProgress', managerId: user?.id }, 'Заявка в работе')}>
                          <Hand className="size-4" />
                        </IconAction>
                      )}
                      {(l.status === 'New' || l.status === 'InProgress') && (
                        <>
                          <IconAction title="Создать клиента" onClick={() => setConvert(l)}>
                            <UserPlus className="size-4" />
                          </IconAction>
                          <IconAction title="Комментарий" onClick={() => setComment(l)}>
                            <MessageSquarePlus className="size-4" />
                          </IconAction>
                          <IconAction title="Отказ" danger onClick={() => setReject(l)}>
                            <Ban className="size-4" />
                          </IconAction>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </Table>
            <Pagination page={page} pageSize={q.data!.pageSize} total={q.data!.totalCount} onChange={setPage} />
          </>
        )}
      </Card>

      <CustomerFormModal open={!!convert} onClose={() => setConvert(null)} lead={convert ? { id: convert.id, name: convert.name, phone: convert.phone } : null} />
      <ConfirmDialog
        open={!!reject}
        onClose={() => setReject(null)}
        danger
        requireReason
        reasonLabel="Почему отказ"
        title="Отметить отказ?"
        confirmLabel="Отказ"
        onConfirm={(reason) => {
          if (reject) update(reject, { status: 'Rejected', comment: reason }, 'Заявка закрыта');
          setReject(null);
        }}
      />
      <CommentModal lead={comment} onClose={() => setComment(null)} onSave={(text) => comment && update(comment, { comment: text }, 'Комментарий сохранён').then(() => setComment(null))} />
      <CreateLeadModal open={create} onClose={() => setCreate(false)} />
    </>
  );
}

function IconAction({ title, onClick, danger, children }: { title: string; onClick: () => void; danger?: boolean; children: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      title={title}
      aria-label={title}
      className={`flex size-8 items-center justify-center rounded-lg text-muted transition hover:scale-105 ${danger ? 'hover:bg-danger-soft hover:text-danger' : 'hover:bg-ink hover:text-white'}`}
    >
      {children}
    </button>
  );
}

function CommentModal({ lead, onClose, onSave }: { lead: LeadResponse | null; onClose: () => void; onSave: (text: string) => void }) {
  const [text, setText] = useState('');
  return (
    <Modal
      open={!!lead}
      onClose={onClose}
      size="sm"
      title="Комментарий к заявке"
      subtitle={lead?.name}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button disabled={!text.trim()} onClick={() => onSave(text.trim())}>Сохранить</Button>
        </>
      }
    >
      {lead?.comment && <p className="mb-3 rounded-xl bg-paper px-4 py-3 text-sm text-muted">Сейчас: {lead.comment}</p>}
      <Textarea label="Новый комментарий" value={text} onChange={(e) => setText(e.target.value)} autoFocus />
    </Modal>
  );
}

function CreateLeadModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [source, setSource] = useState<LeadSource>('Phone');
  const [comment, setComment] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    setLoading(true);
    setError(null);
    try {
      await crm.createLead({ name: name.trim(), phone: normalizePhone(phone), source, comment: comment.trim() || null });
      toast.success('Заявка добавлена');
      queryClient.invalidateQueries({ queryKey: ['crm'] });
      setName('');
      setPhone('');
      setComment('');
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
      title="Новая заявка"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button onClick={submit} loading={loading} disabled={!name.trim() || phone.length < 9}>Добавить</Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Input className="sm:col-span-2" label="Имя" value={name} onChange={(e) => setName(e.target.value)} autoFocus />
        <PhoneInput value={phone} onChange={setPhone} />
        <Select label="Источник" value={source} onChange={(e) => setSource(e.target.value as LeadSource)}>
          {(Object.keys(leadSourceLabel) as LeadSource[]).map((s) => (
            <option key={s} value={s}>{leadSourceLabel[s]}</option>
          ))}
        </Select>
        <Textarea className="sm:col-span-2" label="Комментарий" value={comment} onChange={(e) => setComment(e.target.value)} />
        {error && <p className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger sm:col-span-2">{error}</p>}
      </div>
    </Modal>
  );
}
