import { useMemo } from 'react';
import { useQueries, useQuery } from '@tanstack/react-query';
import { catalog } from '@/api/endpoints';
import { useLang } from '@/api/session';
import type { Apartment } from '@/api/types';

// Застройщик один, на сайте — один дом; если домов станет больше, берём первый как главный.
export function useBuildings() {
  const lang = useLang();
  return useQuery({ queryKey: ['buildings', lang], queryFn: catalog.buildings, staleTime: 30_000 });
}

export function useMainBuilding() {
  const query = useBuildings();
  return { ...query, data: query.data?.items[0] };
}

export function useFloors(buildingId: string | undefined) {
  return useQuery({
    queryKey: ['floors', buildingId],
    queryFn: () => catalog.floors(buildingId!),
    enabled: !!buildingId,
    select: (r) => [...r.items].sort((a, b) => a.number - b.number),
  });
}

// Все квартиры дома: по запросу на этаж (этажей ~17), результаты кэшируются по этажам.
export function useAllApartments(buildingId: string | undefined) {
  const floors = useFloors(buildingId);
  const queries = useQueries({
    queries: (floors.data ?? []).map((f) => ({
      queryKey: ['apartments', f.id],
      queryFn: () => catalog.apartments(f.id),
      staleTime: 30_000,
    })),
  });
  const loading = floors.isLoading || queries.some((q) => q.isLoading);
  const error = floors.error ?? queries.find((q) => q.error)?.error ?? null;
  // Пересчитываем только когда пришли новые данные какого-то этажа.
  const version = queries.map((q) => q.dataUpdatedAt).join(',');
  const data = useMemo(
    () => (loading ? undefined : queries.flatMap((q) => q.data?.items ?? [])),
    // queries — новый массив на каждом рендере, его версия — в version
    [loading, version],
  );
  return { data, floors: floors.data, isLoading: loading, error, refetch: floors.refetch };
}

export interface TypeSummary {
  rooms: number;
  free: number;
  total: number;
  minPrice: number | null;
  minArea: number;
  maxArea: number;
}

export function summarizeByRooms(apartments: Apartment[]): TypeSummary[] {
  const map = new Map<number, TypeSummary>();
  for (const a of apartments) {
    const s = map.get(a.rooms) ?? { rooms: a.rooms, free: 0, total: 0, minPrice: null, minArea: a.area, maxArea: a.area };
    s.total++;
    if (a.status === 'Free') {
      s.free++;
      s.minPrice = s.minPrice === null ? a.price : Math.min(s.minPrice, a.price);
    }
    s.minArea = Math.min(s.minArea, a.area);
    s.maxArea = Math.max(s.maxArea, a.area);
    map.set(a.rooms, s);
  }
  return [...map.values()].sort((a, b) => a.rooms - b.rooms);
}

export function useRate() {
  return useQuery({ queryKey: ['rate'], queryFn: catalog.rate, retry: false, staleTime: 5 * 60_000 });
}
