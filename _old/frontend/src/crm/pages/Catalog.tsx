import { useEffect, useMemo, useState } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, ChevronDown, FileSpreadsheet, Pencil, Plus, Upload, XCircle } from 'lucide-react';
import { ApiError } from '@/api/client';
import { catalog, crm } from '@/api/endpoints';
import type { Apartment, Building, Floor, ImportResult } from '@/api/types';
import { Badge, Button, Card, CardHeader, EmptyState, ErrorState, FileDrop, Input, Modal, PageHeader, Select, Skeleton, StatusBadge, Table, Textarea, useToast } from '@/components/ui';
import { cn } from '@/lib/cn';
import { area, money, roomsShort } from '@/lib/format';
import { ROLES } from '../access';
import { Guard } from '../Guard';

export default function Catalog() {
  return (
    <Guard roles={ROLES.content}>
      <Content />
    </Guard>
  );
}

function Content() {
  const buildings = useQuery({ queryKey: ['crm', 'buildings'], queryFn: catalog.buildings });
  const [buildingId, setBuildingId] = useState<string | null>(null);
  const building = buildings.data?.items.find((b) => b.id === buildingId) ?? buildings.data?.items[0];
  const [editBuilding, setEditBuilding] = useState<Building | 'new' | null>(null);
  const [importOpen, setImportOpen] = useState(false);

  return (
    <>
      <PageHeader
        title="Каталог"
        subtitle="Дом, этажи и квартиры. Статус квартиры меняют только бронь и сделка."
        actions={
          <>
            {building && (
              <Button variant="secondary" icon={<Upload className="size-4" />} onClick={() => setImportOpen(true)}>
                Импорт из Excel
              </Button>
            )}
            <Button icon={<Plus className="size-4" />} onClick={() => setEditBuilding('new')}>
              Новый дом
            </Button>
          </>
        }
      />

      {buildings.error ? (
        <ErrorState error={buildings.error} onRetry={() => buildings.refetch()} />
      ) : buildings.isLoading ? (
        <Skeleton className="h-40 rounded-2xl" />
      ) : !building ? (
        <EmptyState title="Домов пока нет" action={<Button onClick={() => setEditBuilding('new')}>Добавить дом</Button>} />
      ) : (
        <>
          {buildings.data!.items.length > 1 && (
            <Select className="mb-4 w-72" value={building.id} onChange={(e) => setBuildingId(e.target.value)}>
              {buildings.data!.items.map((b) => <option key={b.id} value={b.id}>{b.nameRu}</option>)}
            </Select>
          )}
          <Card className="mb-6 p-6">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <h2 className="font-display text-3xl">{building.nameRu}</h2>
                <p className="mt-1 text-sm text-muted">{building.nameTg} · {building.address}</p>
                <p className="mt-3 max-w-2xl text-sm text-ink/75">{building.descriptionRu ?? <span className="text-muted">Описание не заполнено</span>}</p>
                {!building.descriptionTg && <Badge tone="amber" className="mt-3">Нет описания на таджикском</Badge>}
              </div>
              <div className="flex items-center gap-6">
                <div className="text-right">
                  <p className="display text-4xl tabular">{building.freeApartments}<span className="text-xl text-muted">/{building.totalApartments}</span></p>
                  <p className="text-xs text-muted">свободно</p>
                </div>
                <Button variant="secondary" icon={<Pencil className="size-4" />} onClick={() => setEditBuilding(building)}>Изменить</Button>
              </div>
            </div>
          </Card>
          <FloorsSection building={building} />
          <ImportModal open={importOpen} onClose={() => setImportOpen(false)} buildingId={building.id} />
        </>
      )}

      <BuildingModal value={editBuilding} onClose={() => setEditBuilding(null)} />
    </>
  );
}

function FloorsSection({ building }: { building: Building }) {
  const floors = useQuery({ queryKey: ['floors', building.id], queryFn: () => catalog.floors(building.id), select: (r) => [...r.items].sort((a, b) => b.number - a.number) });
  const [open, setOpen] = useState<string | null>(null);
  const [floorEdit, setFloorEdit] = useState<Floor | 'new' | null>(null);

  return (
    <Card>
      <CardHeader title="Этажи и квартиры" subtitle={floors.data ? `${floors.data.length} этажей` : undefined} action={<Button size="sm" variant="secondary" icon={<Plus className="size-3.5" />} onClick={() => setFloorEdit('new')}>Этаж</Button>} />
      {floors.isLoading ? (
        <div className="flex flex-col gap-2 p-5">{Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-12" />)}</div>
      ) : floors.error ? (
        <ErrorState error={floors.error} onRetry={() => floors.refetch()} />
      ) : (
        <div className="divide-y divide-line/70">
          {floors.data!.map((f) => (
            <div key={f.id}>
              <div className={cn('flex w-full items-center gap-4 px-5 py-3.5 transition-colors hover:bg-paper/60', open === f.id && 'bg-paper/60')}>
                <button onClick={() => setOpen((o) => (o === f.id ? null : f.id))} className="flex flex-1 items-center gap-4 text-left">
                  <span className="w-10 font-display text-2xl tabular">{f.number}</span>
                  <span className="flex-1 text-sm">
                    {f.isCommercial ? <Badge tone="blue">Коммерческий</Badge> : <span className="text-muted">{f.totalApartments} квартир · {f.freeApartments} свободно</span>}
                  </span>
                  <ChevronDown className={cn('size-4 text-muted transition-transform duration-300', open === f.id && 'rotate-180')} />
                </button>
                <button onClick={() => setFloorEdit(f)} className="rounded-lg p-2 text-muted transition hover:bg-white hover:text-ink" title="Изменить этаж" aria-label="Изменить этаж">
                  <Pencil className="size-3.5" />
                </button>
              </div>
              <AnimatePresence initial={false}>
                {open === f.id && (
                  <motion.div initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={{ duration: 0.3 }} className="overflow-hidden bg-paper/40">
                    <FloorApartments floor={f} />
                  </motion.div>
                )}
              </AnimatePresence>
            </div>
          ))}
        </div>
      )}
      <FloorModal value={floorEdit} buildingId={building.id} onClose={() => setFloorEdit(null)} />
    </Card>
  );
}

function FloorApartments({ floor }: { floor: Floor }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const q = useQuery({ queryKey: ['apartments', floor.id], queryFn: () => catalog.apartments(floor.id), select: (r) => [...r.items].sort((a, b) => a.planSlot - b.planSlot) });
  const [edit, setEdit] = useState<Apartment | 'new' | null>(null);

  const toggleOnRequest = async (a: Apartment) => {
    try {
      await crm.setOnRequest(a.id, a.status !== 'OnRequest');
      toast.success(a.status === 'OnRequest' ? `Квартира №${a.number} снова в продаже` : `Квартира №${a.number}: цена по запросу`);
      queryClient.invalidateQueries({ queryKey: ['apartments', floor.id] });
      queryClient.invalidateQueries({ queryKey: ['floors'] });
    } catch (e) {
      toast.error(e);
    }
  };

  return (
    <div className="px-5 pb-5">
      {q.isLoading ? (
        <Skeleton className="h-24" />
      ) : (
        <div className="overflow-hidden rounded-xl border border-line/70 bg-white">
          <Table head={['№', 'Тип', 'Площадь', 'Цена за м²', 'Цена', 'Позиция', 'Статус', '']}>
            {q.data!.map((a) => (
              <tr key={a.id}>
                <td className="font-display text-lg tabular">{a.number}</td>
                <td>{roomsShort(a.rooms)}</td>
                <td className="tabular">{area(a.area)}</td>
                <td className="tabular">{money(a.pricePerM2)}</td>
                <td className="font-semibold tabular">{money(a.price)}</td>
                <td className="tabular text-muted">{a.planSlot + 1}{a.model3dId && <span className="ml-1 text-[11px]">· 3D</span>}</td>
                <td><StatusBadge status={a.status} /></td>
                <td>
                  <div className="flex justify-end gap-1">
                    {(a.status === 'Free' || a.status === 'OnRequest') && (
                      <button onClick={() => toggleOnRequest(a)} className="rounded-lg px-2 py-1 text-xs text-muted transition hover:bg-paper hover:text-ink">
                        {a.status === 'OnRequest' ? 'В продажу' : 'Под запрос'}
                      </button>
                    )}
                    <button onClick={() => setEdit(a)} className="rounded-lg p-1.5 text-muted transition hover:bg-ink hover:text-white" aria-label="Изменить">
                      <Pencil className="size-3.5" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </Table>
          <button onClick={() => setEdit('new')} className="flex w-full items-center justify-center gap-1.5 border-t border-line/70 py-3 text-sm text-muted transition hover:bg-paper hover:text-ink">
            <Plus className="size-4" /> Добавить квартиру
          </button>
        </div>
      )}
      <ApartmentModal value={edit} floor={floor} onClose={() => setEdit(null)} />
    </div>
  );
}

function ApartmentModal({ value, floor, onClose }: { value: Apartment | 'new' | null; floor: Floor; onClose: () => void }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const existing = value && value !== 'new' ? value : null;
  const [form, setForm] = useState({ number: '', rooms: '1', area: '', pricePerM2: '', planSlot: '1', model3dId: '' });
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!value) return;
    setError(null);
    setForm(
      existing
        ? { number: String(existing.number), rooms: String(existing.rooms), area: String(existing.area), pricePerM2: String(existing.pricePerM2), planSlot: String(existing.planSlot + 1), model3dId: existing.model3dId ?? '' }
        : { number: '', rooms: '1', area: '', pricePerM2: '', planSlot: '1', model3dId: '' },
    );
  }, [value, existing]);

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => setForm((f) => ({ ...f, [k]: e.target.value }));
  const num = (v: string) => Number(v.replace(',', '.'));
  const preview = num(form.area) * num(form.pricePerM2);

  const submit = async () => {
    setLoading(true);
    setError(null);
    try {
      await crm.saveApartment(
        {
          floorId: existing ? undefined : floor.id,
          number: num(form.number),
          rooms: num(form.rooms),
          area: num(form.area),
          pricePerM2: num(form.pricePerM2),
          planSlot: num(form.planSlot) - 1,
          model3dId: form.model3dId.trim() || null,
        },
        existing?.id,
      );
      toast.success(existing ? 'Квартира сохранена' : 'Квартира добавлена');
      queryClient.invalidateQueries({ queryKey: ['apartments'] });
      queryClient.invalidateQueries({ queryKey: ['floors'] });
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
      title={existing ? `Квартира №${existing.number}` : `Новая квартира · этаж ${floor.number}`}
      subtitle="Изменение цены попадёт в журнал"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button loading={loading} onClick={submit}>Сохранить</Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Input label="Номер" inputMode="numeric" value={form.number} onChange={set('number')} error={error?.field('number')} />
        <Select label="Комнат" value={form.rooms} onChange={set('rooms')}>
          {[0, 1, 2, 3, 4, 5].map((r) => <option key={r} value={r}>{r === 0 ? 'Студия' : r}</option>)}
        </Select>
        <Input label="Площадь" inputMode="decimal" value={form.area} onChange={set('area')} suffix="м²" error={error?.field('area')} />
        <Input label="Цена за м²" inputMode="decimal" value={form.pricePerM2} onChange={set('pricePerM2')} suffix="сом." error={error?.field('pricePerM2')} />
        <Select label="Позиция на плане" value={form.planSlot} onChange={set('planSlot')} hint="1–4 — северная сторона, 5–8 — южная">
          {Array.from({ length: 8 }, (_, i) => <option key={i} value={i + 1}>{i + 1}</option>)}
        </Select>
        <Input label="ID в 3D-модели" value={form.model3dId} onChange={set('model3dId')} hint="необязательно" error={error?.field('model3dId')} />
      </div>
      {preview > 0 && (
        <p className="mt-4 rounded-xl bg-paper px-4 py-3 text-sm">
          Цена ≈ <span className="font-semibold tabular">{money(Math.round(preview / 100) * 100)}</span> <span className="text-muted">(округляется до 100 сомони)</span>
        </p>
      )}
      {error && !Object.keys(error.fieldErrors).length && <p className="mt-4 rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger">{error.message}</p>}
    </Modal>
  );
}

function FloorModal({ value, buildingId, onClose }: { value: Floor | 'new' | null; buildingId: string; onClose: () => void }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const existing = value && value !== 'new' ? value : null;
  const [number, setNumber] = useState('');
  const [plan, setPlan] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!value) return;
    setNumber(existing ? String(existing.number) : '');
    setPlan(existing?.planImageUrl ?? '');
    setError(null);
  }, [value, existing]);

  const submit = async () => {
    setLoading(true);
    setError(null);
    try {
      if (existing) await crm.updateFloor(existing.id, Number(number), plan.trim() || null);
      else await crm.createFloor(buildingId, Number(number), plan.trim() || null);
      toast.success('Этаж сохранён');
      queryClient.invalidateQueries({ queryKey: ['floors'] });
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Ошибка');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      open={!!value}
      onClose={onClose}
      size="sm"
      title={existing ? `Этаж ${existing.number}` : 'Новый этаж'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button loading={loading} disabled={number === ''} onClick={submit}>Сохранить</Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <Input label="Номер этажа" type="number" min={0} value={number} onChange={(e) => setNumber(e.target.value)} hint="0 — коммерческий этаж" />
        <Input label="Ссылка на план этажа" value={plan} onChange={(e) => setPlan(e.target.value)} hint="необязательно" />
        {error && <p className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger">{error}</p>}
      </div>
    </Modal>
  );
}

function BuildingModal({ value, onClose }: { value: Building | 'new' | null; onClose: () => void }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const existing = value && value !== 'new' ? value : null;
  const empty = { nameRu: '', nameTg: '', address: '', floorsCount: '16', descriptionRu: '', descriptionTg: '', modelUrl: '' };
  const [form, setForm] = useState(empty);
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!value) return;
    setError(null);
    setForm(
      existing
        ? {
            nameRu: existing.nameRu,
            nameTg: existing.nameTg,
            address: existing.address,
            floorsCount: String(existing.floorsCount),
            descriptionRu: existing.descriptionRu ?? '',
            descriptionTg: existing.descriptionTg ?? '',
            modelUrl: existing.modelUrl ?? '',
          }
        : empty,
    );
    // empty — константа формы, менять её не нужно
  }, [value, existing]);

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => setForm((f) => ({ ...f, [k]: e.target.value }));

  const submit = async () => {
    setLoading(true);
    setError(null);
    try {
      await crm.saveBuilding(
        {
          nameRu: form.nameRu.trim(),
          nameTg: form.nameTg.trim(),
          address: form.address.trim(),
          floorsCount: Number(form.floorsCount),
          descriptionRu: form.descriptionRu.trim() || null,
          descriptionTg: form.descriptionTg.trim() || null,
          modelUrl: form.modelUrl.trim() || null,
        },
        existing?.id,
      );
      toast.success('Дом сохранён');
      queryClient.invalidateQueries({ queryKey: ['crm', 'buildings'] });
      queryClient.invalidateQueries({ queryKey: ['buildings'] });
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
      size="lg"
      title={existing ? 'Дом' : 'Новый дом'}
      subtitle="Название и описание на двух языках: сайт показывает их по выбранному языку"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Отмена</Button>
          <Button loading={loading} onClick={submit}>Сохранить</Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Input label="Название (рус.)" value={form.nameRu} onChange={set('nameRu')} error={error?.field('nameRu')} />
        <Input label="Название (тоҷ.)" value={form.nameTg} onChange={set('nameTg')} error={error?.field('nameTg')} />
        <Input label="Адрес" value={form.address} onChange={set('address')} error={error?.field('address')} />
        <Input label="Этажей" type="number" min={1} value={form.floorsCount} onChange={set('floorsCount')} error={error?.field('floorsCount')} />
        <Textarea label="Описание (рус.)" value={form.descriptionRu} onChange={set('descriptionRu')} rows={4} error={error?.field('descriptionRu')} />
        <Textarea label="Описание (тоҷ.)" value={form.descriptionTg} onChange={set('descriptionTg')} rows={4} error={error?.field('descriptionTg')} />
        <Input className="sm:col-span-2" label="Ссылка на 3D-модель" value={form.modelUrl} onChange={set('modelUrl')} hint="необязательно; пока модели нет, сайт строит дом по этажам каталога" />
        {error && !Object.keys(error.fieldErrors).length && <p className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger sm:col-span-2">{error.message}</p>}
      </div>
    </Modal>
  );
}

function ImportModal({ open, onClose, buildingId }: { open: boolean; onClose: () => void; buildingId: string }) {
  const queryClient = useQueryClient();
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const columns = useMemo(() => ['A — этаж', 'B — номер', 'C — комнат (0 — студия)', 'D — площадь', 'E — цена за м²', 'F — позиция на плане', 'G — ID в 3D (необяз.)'], []);

  const submit = async () => {
    if (!file) return;
    setLoading(true);
    setError(null);
    setResult(null);
    const form = new FormData();
    form.append('file', file);
    try {
      const r = await crm.importApartments(buildingId, form);
      setResult(r);
      queryClient.invalidateQueries({ queryKey: ['apartments'] });
      queryClient.invalidateQueries({ queryKey: ['floors'] });
    } catch (e) {
      // Ошибки по строкам сервер возвращает с кодом 422 в теле ImportResult.
      const rows = e instanceof ApiError && e.status === 422 ? (e.body as ImportResult | undefined)?.errors : undefined;
      if (rows?.length) setError(rows.map((r) => `Строка ${r.row}: ${r.message}`).join('\n'));
      else setError(e instanceof Error ? e.message : 'Ошибка импорта');
    } finally {
      setLoading(false);
    }
  };

  const close = () => {
    onClose();
    setTimeout(() => {
      setFile(null);
      setResult(null);
      setError(null);
    }, 300);
  };

  return (
    <Modal
      open={open}
      onClose={close}
      size="lg"
      title="Импорт квартир из Excel"
      subtitle="При любой ошибке ничего не сохраняется"
      footer={
        <>
          <Button variant="secondary" onClick={close}>Закрыть</Button>
          <Button loading={loading} disabled={!file} icon={<FileSpreadsheet className="size-4" />} onClick={submit}>Импортировать</Button>
        </>
      }
    >
      <div className="mb-4 flex flex-wrap gap-1.5">
        {columns.map((c) => <Badge key={c}>{c}</Badge>)}
      </div>
      <FileDrop accept=".xlsx" file={file} onFile={(f) => { setFile(f); setResult(null); setError(null); }} hint="Файл .xlsx, первая строка — заголовки" />
      <AnimatePresence>
        {result && (
          <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} className="mt-4 flex items-center gap-3 rounded-2xl bg-free-soft px-4 py-3 text-sm text-free">
            <CheckCircle2 className="size-5" /> Создано {result.created}, обновлено {result.updated}
          </motion.div>
        )}
        {error && (
          <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} className="mt-4 flex items-start gap-3 rounded-2xl bg-danger-soft px-4 py-3 text-sm text-danger">
            <XCircle className="mt-0.5 size-5 shrink-0" /> <span className="whitespace-pre-wrap">{error}</span>
          </motion.div>
        )}
      </AnimatePresence>
    </Modal>
  );
}
