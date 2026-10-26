import { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ImagePlus, Pencil, Plus } from 'lucide-react';
import { ApiError } from '@/api/client';
import { catalog, crm } from '@/api/endpoints';
import type { Stage } from '@/api/types';
import { Button, ErrorState, FileDrop, Input, Modal, PageHeader, Skeleton, useToast } from '@/components/ui';
import { todayIso } from '@/lib/format';
import { ConstructionView } from '@/shared/ConstructionView';
import { ROLES, useStaff } from '../access';
import { Guard } from '../Guard';

export default function Construction() {
  return (
    <Guard roles={ROLES.construction}>
      <Content />
    </Guard>
  );
}

function Content() {
  const staff = useStaff();
  const editor = staff.can(ROLES.content);
  const buildings = useQuery({ queryKey: ['crm', 'buildings'], queryFn: catalog.buildings });
  const building = buildings.data?.items[0];
  const q = useQuery({ queryKey: ['crm', 'stages', building?.id], queryFn: () => crm.stages(building!.id), enabled: !!building });
  const [edit, setEdit] = useState<Stage | 'new' | null>(null);
  const [photo, setPhoto] = useState<Stage | null>(null);
  const stages = q.data?.stages ?? [];

  return (
    <>
      <PageHeader
        title="Ход стройки"
        subtitle={building ? `${building.nameRu} · покупатели видят этапы и фото в кабинете` : undefined}
        actions={editor && building && <Button icon={<Plus className="size-4" />} onClick={() => setEdit('new')}>Новый этап</Button>}
      />
      {q.error ? (
        <ErrorState error={q.error} onRetry={() => q.refetch()} />
      ) : !q.data ? (
        <Skeleton className="h-96 rounded-2xl" />
      ) : (
        <ConstructionView
          progress={q.data}
          actions={
            editor
              ? (stageId) => {
                  const s = stages.find((x) => x.id === stageId)!;
                  return (
                    <div className="flex gap-1">
                      <button onClick={() => setPhoto(s)} className="rounded-lg p-2 text-muted transition hover:bg-ink hover:text-white" title="Добавить фото" aria-label="Добавить фото">
                        <ImagePlus className="size-4" />
                      </button>
                      <button onClick={() => setEdit(s)} className="rounded-lg p-2 text-muted transition hover:bg-ink hover:text-white" title="Изменить этап" aria-label="Изменить этап">
                        <Pencil className="size-4" />
                      </button>
                    </div>
                  );
                }
              : undefined
          }
        />
      )}
      {building && <StageModal value={edit} buildingId={building.id} nextOrder={stages.length + 1} onClose={() => setEdit(null)} />}
      <PhotoModal stage={photo} onClose={() => setPhoto(null)} />
    </>
  );
}

function StageModal({ value, buildingId, nextOrder, onClose }: { value: Stage | 'new' | null; buildingId: string; nextOrder: number; onClose: () => void }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const existing = value && value !== 'new' ? value : null;
  const [name, setName] = useState('');
  const [percent, setPercent] = useState(0);
  const [order, setOrder] = useState('1');
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!value) return;
    setName(existing?.name ?? '');
    setPercent(existing?.percent ?? 0);
    setOrder(String(existing?.sortOrder ?? nextOrder));
    setError(null);
  }, [value, existing, nextOrder]);

  const submit = async () => {
    setLoading(true);
    setError(null);
    try {
      const body = { name: name.trim(), percent, sortOrder: Number(order) };
      if (existing) await crm.updateStage(existing.id, body);
      else await crm.createStage(buildingId, body);
      toast.success('Этап сохранён');
      queryClient.invalidateQueries({ queryKey: ['crm', 'stages'] });
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e : new ApiError(0, 'Ошибка'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      open={!!value}
      onClose={onClose}
      size="sm"
      title={existing ? 'Этап стройки' : 'Новый этап'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button loading={loading} disabled={!name.trim()} onClick={submit}>Сохранить</Button>
        </>
      }
    >
      <div className="flex flex-col gap-5">
        <Input label="Название" placeholder="Например: Монолитный каркас" value={name} onChange={(e) => setName(e.target.value)} autoFocus error={error?.field('name')} />
        <div>
          <div className="mb-2 flex items-baseline justify-between">
            <span className="text-[13px] font-medium text-ink/80">Готовность</span>
            <span className="display text-3xl tabular">{percent}%</span>
          </div>
          <input
            type="range"
            min={0}
            max={100}
            step={5}
            value={percent}
            onChange={(e) => setPercent(Number(e.target.value))}
            className="h-1.5 w-full cursor-pointer appearance-none rounded-full [&::-webkit-slider-thumb]:size-5 [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-ink"
            style={{ background: `linear-gradient(to right, var(--color-gold) ${percent}%, #ebe6dd ${percent}%)` }}
          />
        </div>
        <Input label="Порядок" type="number" min={1} value={order} onChange={(e) => setOrder(e.target.value)} error={error?.field('sortOrder')} />
        {error && !Object.keys(error.fieldErrors).length && <p className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger">{error.message}</p>}
      </div>
    </Modal>
  );
}

function PhotoModal({ stage, onClose }: { stage: Stage | null; onClose: () => void }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [file, setFile] = useState<File | null>(null);
  const [caption, setCaption] = useState('');
  const [takenAt, setTakenAt] = useState(todayIso());
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (!stage || !file) return;
    setLoading(true);
    setError(null);
    const form = new FormData();
    form.append('file', file);
    if (caption.trim()) form.append('caption', caption.trim());
    form.append('takenAt', takenAt);
    try {
      await crm.uploadPhoto(stage.id, form);
      toast.success('Фото добавлено');
      queryClient.invalidateQueries({ queryKey: ['crm', 'stages'] });
      setFile(null);
      setCaption('');
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Ошибка');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      open={!!stage}
      onClose={onClose}
      title="Фото стройки"
      subtitle={stage?.name}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button loading={loading} disabled={!file} onClick={submit}>Загрузить</Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <FileDrop accept=".png,.jpg,.jpeg" file={file} onFile={setFile} hint="PNG или JPG до 10 МБ" />
        <div className="grid gap-4 sm:grid-cols-2">
          <Input label="Подпись" value={caption} onChange={(e) => setCaption(e.target.value)} />
          <Input label="Дата съёмки" type="date" max={todayIso()} value={takenAt} onChange={(e) => setTakenAt(e.target.value)} />
        </div>
        {error && <p className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger">{error}</p>}
      </div>
    </Modal>
  );
}
