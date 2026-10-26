import type { ReactNode } from 'react';
import { ShieldOff } from 'lucide-react';
import type { Role } from '@/api/types';
import { ButtonLink, EmptyState } from '@/components/ui';
import { homeFor, useStaff } from './access';

// Страница видна только нужным ролям; сервер всё равно проверяет права в каждом запросе.
export function Guard({ roles, children }: { roles: Role[]; children: ReactNode }) {
  const staff = useStaff();
  if (staff.can(roles)) return <>{children}</>;
  return (
    <EmptyState
      className="min-h-[60vh]"
      icon={<ShieldOff className="size-6" />}
      title="Нет доступа к этому разделу"
      text="Раздел доступен другим ролям. Если доступ нужен, обратитесь к администратору."
      action={<ButtonLink to={homeFor(staff.roles)}>На мою стартовую страницу</ButtonLink>}
    />
  );
}
