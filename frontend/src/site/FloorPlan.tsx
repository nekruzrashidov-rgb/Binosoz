import { useMemo } from 'react';
import { motion } from 'motion/react';
import type { Apartment, ApartmentStatus } from '@/api/types';
import { cn } from '@/lib/cn';
import { roomsShort } from '@/lib/format';

// Схема этажа: коридор посередине, позиции 0–3 — северная сторона, 4–7 — южная.
// Ширина квартиры пропорциональна площади, чтобы схема была похожа на реальную.
const VB_W = 1000;
const VB_H = 520;
const PAD = 24;
const CORRIDOR = 76;
const ROW_H = (VB_H - PAD * 2 - CORRIDOR) / 2;

const fill: Record<ApartmentStatus, string> = {
  Free: '#e3f4ec',
  Booked: '#fbf0dc',
  Sold: '#eef0f3',
  OnRequest: '#e6edfb',
};
const stroke: Record<ApartmentStatus, string> = {
  Free: '#2f9e6e',
  Booked: '#d99a2b',
  Sold: '#b9bec6',
  OnRequest: '#4a74d9',
};

interface Cell {
  apartment: Apartment;
  x: number;
  y: number;
  w: number;
  h: number;
}

export function FloorPlan({
  apartments,
  hoveredId,
  onHover,
  onSelect,
  className,
}: {
  apartments: Apartment[];
  hoveredId?: string | null;
  onHover?: (a: Apartment | null) => void;
  onSelect?: (a: Apartment) => void;
  className?: string;
}) {
  const cells = useMemo(() => layout(apartments), [apartments]);

  return (
    <svg viewBox={`0 0 ${VB_W} ${VB_H}`} className={cn('h-auto w-full select-none', className)} role="img" aria-label="План этажа">
      <defs>
        <pattern id="hatch" width="8" height="8" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
          <line x1="0" y1="0" x2="0" y2="8" stroke="#d5d9df" strokeWidth="2" />
        </pattern>
        <filter id="lift" x="-20%" y="-20%" width="140%" height="140%">
          <feDropShadow dx="0" dy="10" stdDeviation="12" floodColor="#0f1115" floodOpacity="0.18" />
        </filter>
      </defs>

      {/* Контур здания и коридор */}
      <rect x={PAD - 8} y={PAD - 8} width={VB_W - PAD * 2 + 16} height={VB_H - PAD * 2 + 16} rx={18} fill="#fff" stroke="#1b1e25" strokeWidth={3} />
      <rect x={PAD} y={PAD + ROW_H} width={VB_W - PAD * 2} height={CORRIDOR} fill="#f6f3ee" />
      <g fill="#b9b3a8" fontSize="13" fontFamily="Inter Variable, sans-serif" letterSpacing="3">
        <text x={VB_W / 2} y={PAD + ROW_H + CORRIDOR / 2 + 5} textAnchor="middle">КОРИДОР</text>
      </g>
      {/* Лифты и лестница */}
      <g transform={`translate(${VB_W / 2 - 60}, ${PAD + ROW_H + 14})`}>
        <rect width="34" height="48" rx="4" fill="#ebe6dd" />
        <rect x="42" width="34" height="48" rx="4" fill="#ebe6dd" />
        <rect x="84" width="36" height="48" rx="4" fill="url(#hatch)" stroke="#d5d9df" />
      </g>

      {cells.map((c, i) => {
        const a = c.apartment;
        const active = hoveredId === a.id;
        const sold = a.status === 'Sold';
        return (
          <motion.g
            key={a.id}
            initial={{ opacity: 0, scale: 0.94 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ delay: 0.05 * i, duration: 0.4, ease: [0.22, 1, 0.36, 1] }}
            style={{ transformOrigin: `${c.x + c.w / 2}px ${c.y + c.h / 2}px`, cursor: 'pointer' }}
            onMouseEnter={() => onHover?.(a)}
            onMouseLeave={() => onHover?.(null)}
            onClick={() => onSelect?.(a)}
            role="button"
            aria-label={`Квартира ${a.number}`}
          >
            <motion.rect
              x={c.x + 4}
              y={c.y + 4}
              width={c.w - 8}
              height={c.h - 8}
              rx={10}
              // attrY — атрибут y; обычный y в motion у SVG означает сдвиг трансформом.
              animate={{
                fill: active ? stroke[a.status] : fill[a.status],
                attrY: active ? c.y - 2 : c.y + 4,
              }}
              transition={{ duration: 0.2 }}
              stroke={stroke[a.status]}
              strokeWidth={active ? 0 : 1.5}
              filter={active ? 'url(#lift)' : undefined}
            />
            {sold && !active && <rect x={c.x + 4} y={c.y + 4} width={c.w - 8} height={c.h - 8} rx={10} fill="url(#hatch)" opacity={0.6} pointerEvents="none" />}
            <motion.g animate={{ y: active ? -6 : 0 }} transition={{ duration: 0.2 }} pointerEvents="none">
              <text
                x={c.x + c.w / 2}
                y={c.y + c.h / 2 - 6}
                textAnchor="middle"
                fontFamily="Playfair Display, serif"
                fontSize={c.w > 150 ? 34 : 28}
                fill={active ? '#fff' : sold ? '#9aa0aa' : '#0f1115'}
              >
                {a.number}
              </text>
              <text x={c.x + c.w / 2} y={c.y + c.h / 2 + 22} textAnchor="middle" fontFamily="Inter Variable, sans-serif" fontSize="14" fill={active ? 'rgba(255,255,255,0.85)' : '#6b6f78'}>
                {roomsShort(a.rooms)} · {a.area} м²
              </text>
            </motion.g>
          </motion.g>
        );
      })}
    </svg>
  );
}

function layout(apartments: Apartment[]): Cell[] {
  const rows: Apartment[][] = [[], []];
  for (const a of apartments) rows[a.planSlot < 4 ? 0 : 1].push(a);
  const usable = VB_W - PAD * 2;
  const cells: Cell[] = [];
  rows.forEach((row, r) => {
    const sorted = [...row].sort((a, b) => a.planSlot - b.planSlot);
    const total = sorted.reduce((s, a) => s + a.area, 0) || 1;
    let x = PAD;
    const y = r === 0 ? PAD : PAD + ROW_H + CORRIDOR;
    for (const a of sorted) {
      const w = (a.area / total) * usable;
      cells.push({ apartment: a, x, y, w, h: ROW_H });
      x += w;
    }
  });
  return cells;
}
