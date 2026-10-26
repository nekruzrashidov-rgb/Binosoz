import { Compass } from 'lucide-react';
import { ButtonLink, EmptyState } from '@/components/ui';
import { homeFor, useStaff } from '../access';

export default function CrmNotFound() {
  const { roles } = useStaff();
  return (
    <EmptyState
      className="min-h-[60vh]"
      icon={<Compass className="size-6" />}
      title="Такого раздела нет"
      action={<ButtonLink to={homeFor(roles)}>На стартовую страницу</ButtonLink>}
    />
  );
}
