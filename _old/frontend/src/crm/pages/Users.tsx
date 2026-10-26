import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Eye, EyeOff, Lock, Plus, Unlock, UserCog } from 'lucide-react';
import { ApiError } from '@/api/client';
import { crm } from '@/api/endpoints';
import type { Role, UserResponse } from '@/api/types';
import { Badge, Button, Card, ConfirmDialog, EmptyState, ErrorState, Input, Modal, PageHeader, Pagination, PhoneInput, Select, Skeleton, Table, useToast } from '@/components/ui';
import { dateShort, formatPhone, normalizePhone, roleLabel } from '@/lib/format';
import { ROLES, useStaff } from '../access';
import { Guard } from '../Guard';

export default function Users() {
  return (
    <Guard roles={ROLES.admin}>
      <Content />
    </Guard>
  );
}

function Content() {
  const queryClient = useQueryClient();
  const toast = useToast();
  const { user: me } = useStaff();
  const [page, setPage] = useState(1);
  const q = useQuery({ queryKey: ['crm', 'users', page], queryFn: () => crm.users(page) });
  const [create, setCreate] = useState(false);
  const [toggle, setToggle] = useState<UserResponse | null>(null);
  const [busy, setBusy] = useState(false);
  // Покупатели заводятся сами при входе по SMS — в списке сотрудников их не показываем.
  const staff = q.data?.items.filter((u) => !u.roles.every((r) => r === 'Customer')) ?? [];

  return (
    <>
      <PageHeader title="Сотрудники" subtitle="Доступ в CRM по ролям" actions={<Button icon={<Plus className="size-4" />} onClick={() => setCreate(true)}>Добавить сотрудника</Button>} />
      <Card>
        {q.error ? (
          <ErrorState error={q.error} onRetry={() => q.refetch()} />
        ) : !q.data ? (
          <div className="flex flex-col gap-2 p-5">{Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-12" />)}</div>
        ) : staff.length === 0 ? (
          <EmptyState icon={<UserCog className="size-6" />} title="Сотрудников нет" />
        ) : (
          <>
            <Table head={['Сотрудник', 'Роль', 'Телефон', 'Создан', 'Статус', '']}>
              {staff.map((u) => (
                <tr key={u.id} className={u.isActive ? '' : 'opacity-60'}>
                  <td>
                    <p className="font-medium">{u.fullName} {u.id === me?.id && <span className="text-xs text-muted">(вы)</span>}</p>
                    <p className="text-xs text-muted">{u.email}</p>
                  </td>
                  <td>
                    <div className="flex flex-wrap gap-1">{u.roles.map((r) => <Badge key={r} tone={r === 'Admin' ? 'dark' : 'neutral'}>{roleLabel[r]}</Badge>)}</div>
                  </td>
                  <td className="tabular text-muted">{u.phone ? formatPhone(u.phone) : '—'}</td>
                  <td className="tabular text-muted">{dateShort(u.createdAt)}</td>
                  <td>{u.isActive ? <Badge tone="green">Активен</Badge> : <Badge tone="red">Заблокирован</Badge>}</td>
                  <td className="text-right">
                    {u.id !== me?.id && (
                      <Button size="sm" variant={u.isActive ? 'ghost' : 'secondary'} icon={u.isActive ? <Lock className="size-3.5" /> : <Unlock className="size-3.5" />} onClick={() => setToggle(u)}>
                        {u.isActive ? 'Заблокировать' : 'Разблокировать'}
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </Table>
            <Pagination page={page} pageSize={q.data.pageSize} total={q.data.totalCount} onChange={setPage} />
          </>
        )}
      </Card>

      <CreateUserModal open={create} onClose={() => setCreate(false)} />
      <ConfirmDialog
        open={!!toggle}
        onClose={() => setToggle(null)}
        loading={busy}
        danger={toggle?.isActive}
        title={toggle?.isActive ? `Заблокировать ${toggle?.fullName}?` : `Разблокировать ${toggle?.fullName}?`}
        text={toggle?.isActive ? 'Вход закроется сразу, все сессии будут отозваны. Уже выданный токен действует до 15 минут.' : 'Сотрудник снова сможет войти.'}
        confirmLabel={toggle?.isActive ? 'Заблокировать' : 'Разблокировать'}
        onConfirm={async () => {
          if (!toggle) return;
          setBusy(true);
          try {
            await crm.setUserStatus(toggle.id, !toggle.isActive);
            toast.success(toggle.isActive ? 'Сотрудник заблокирован' : 'Сотрудник разблокирован');
            setToggle(null);
            queryClient.invalidateQueries({ queryKey: ['crm', 'users'] });
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

function CreateUserModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [role, setRole] = useState<Role>('Manager');
  const [password, setPassword] = useState('');
  const [show, setShow] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(false);

  const submit = async () => {
    setLoading(true);
    setError(null);
    try {
      await crm.createUser({ fullName: fullName.trim(), email: email.trim(), role, password, phone: phone ? normalizePhone(phone) : null });
      toast.success('Сотрудник добавлен. Передайте ему пароль лично.');
      queryClient.invalidateQueries({ queryKey: ['crm', 'users'] });
      setFullName('');
      setEmail('');
      setPhone('');
      setPassword('');
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
      title="Новый сотрудник"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button loading={loading} onClick={submit}>Добавить</Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Input className="sm:col-span-2" label="ФИО" value={fullName} onChange={(e) => setFullName(e.target.value)} autoFocus error={error?.field('fullName')} />
        <Input label="Почта (логин)" type="email" value={email} onChange={(e) => setEmail(e.target.value)} error={error?.field('email')} />
        <Select label="Роль" value={role} onChange={(e) => setRole(e.target.value as Role)} error={error?.field('role')}>
          {(['Manager', 'Accountant', 'Content', 'Admin'] as Role[]).map((r) => <option key={r} value={r}>{roleLabel[r]}</option>)}
        </Select>
        <PhoneInput label="Телефон (необязательно)" value={phone} onChange={setPhone} error={error?.field('phone')} />
        <Input
          label="Пароль"
          type={show ? 'text' : 'password'}
          autoComplete="new-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          hint="От 8 символов: цифра, строчная и заглавная буква"
          error={error?.field('password')}
          suffix={
            <button type="button" onClick={() => setShow((v) => !v)} className="text-muted hover:text-ink" aria-label={show ? 'Скрыть пароль' : 'Показать пароль'}>
              {show ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
            </button>
          }
        />
        {error && !Object.keys(error.fieldErrors).length && <p className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger sm:col-span-2">{error.message}</p>}
      </div>
    </Modal>
  );
}
