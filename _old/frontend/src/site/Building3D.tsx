import { useMemo, useRef, useState } from 'react';
import { Canvas, useFrame } from '@react-three/fiber';
import { ContactShadows, Html, OrbitControls, RoundedBox } from '@react-three/drei';
import * as THREE from 'three';
import type { Floor } from '@/api/types';

// Процедурная модель дома: модели застройщика пока нет (Building.ModelUrl пустой),
// поэтому дом строится из этажей каталога — у каждого этажа свой цвет доступности.

const W = 11; // ширина фасада
const D = 6.5; // глубина
const H = 1; // высота жилого этажа
const PODIUM_H = 1.6; // коммерческий этаж выше жилого

const GOLD = new THREE.Color('#c9a46a');
const FREE = new THREE.Color('#5fd39c');

interface Props {
  floors: Floor[];
  selectedId?: string | null;
  onSelect?: (floor: Floor) => void;
  onHover?: (floor: Floor | null) => void;
  autoRotate?: boolean;
  interactive?: boolean;
  showLabels?: boolean;
}

export function Building3D({ floors, selectedId, onSelect, onHover, autoRotate = true, interactive = true, showLabels = true }: Props) {
  const sorted = useMemo(() => [...floors].sort((a, b) => a.number - b.number), [floors]);
  const residential = sorted.filter((f) => !f.isCommercial).length;
  const totalHeight = PODIUM_H + residential * H + 0.8;
  const [hovered, setHovered] = useState<string | null>(null);

  return (
    <Canvas
      shadows
      dpr={[1, 2]}
      camera={{ position: [22, totalHeight * 0.75, 26], fov: 32 }}
      gl={{ antialias: true, alpha: true, toneMapping: THREE.ACESFilmicToneMapping }}
      onPointerMissed={() => {
        setHovered(null);
        onHover?.(null);
      }}
    >
      <hemisphereLight args={['#fff6e8', '#2a2e37', 0.9]} />
      <directionalLight
        position={[18, 30, 14]}
        intensity={2.2}
        castShadow
        shadow-mapSize={[2048, 2048]}
        shadow-camera-left={-20}
        shadow-camera-right={20}
        shadow-camera-top={30}
        shadow-camera-bottom={-10}
      />
      <directionalLight position={[-14, 10, -10]} intensity={0.6} color="#9db4ff" />

      <group position={[0, -totalHeight / 2, 0]}>
        {sorted.map((floor) => {
          const y = floor.isCommercial ? 0 : PODIUM_H + (floor.number - 1) * H;
          return (
            <FloorBlock
              key={floor.id}
              floor={floor}
              y={y}
              height={floor.isCommercial ? PODIUM_H : H}
              hovered={hovered === floor.id}
              selected={selectedId === floor.id}
              interactive={interactive}
              showLabel={showLabels}
              onOver={() => {
                setHovered(floor.id);
                onHover?.(floor);
              }}
              onOut={() => {
                setHovered((h) => (h === floor.id ? null : h));
                onHover?.(null);
              }}
              onClick={() => onSelect?.(floor)}
            />
          );
        })}
        <Roof y={PODIUM_H + residential * H} />
        <ContactShadows position={[0, -0.01, 0]} scale={40} blur={2.4} opacity={0.45} far={20} />
      </group>

      <OrbitControls
        enablePan={false}
        enableZoom={interactive}
        minDistance={18}
        maxDistance={52}
        minPolarAngle={Math.PI / 4}
        maxPolarAngle={Math.PI / 2.05}
        autoRotate={autoRotate && !hovered}
        autoRotateSpeed={0.55}
        enableDamping
        dampingFactor={0.08}
      />
    </Canvas>
  );
}

function FloorBlock({
  floor,
  y,
  height,
  hovered,
  selected,
  interactive,
  showLabel,
  onOver,
  onOut,
  onClick,
}: {
  floor: Floor;
  y: number;
  height: number;
  hovered: boolean;
  selected: boolean;
  interactive: boolean;
  showLabel: boolean;
  onOver: () => void;
  onOut: () => void;
  onClick: () => void;
}) {
  const glass = useRef<THREE.MeshStandardMaterial>(null);
  const group = useRef<THREE.Group>(null);
  const ratio = floor.totalApartments ? floor.freeApartments / floor.totalApartments : 0;
  const active = hovered || selected;

  // Плавная подсветка стекла и «выдвижение» этажа при наведении.
  useFrame((_, dt) => {
    const k = Math.min(1, dt * 10);
    if (glass.current) {
      const target = active ? GOLD : floor.freeApartments > 0 ? FREE.clone().lerp(new THREE.Color('#9fb7c9'), 1 - ratio * 0.9) : new THREE.Color('#8f99a6');
      glass.current.color.lerp(target, k);
      glass.current.emissive.lerp(active ? GOLD : new THREE.Color('#000000'), k);
      glass.current.emissiveIntensity = THREE.MathUtils.lerp(glass.current.emissiveIntensity, active ? 0.55 : 0, k);
    }
    if (group.current) {
      const s = active ? 1.025 : 1;
      group.current.scale.x = THREE.MathUtils.lerp(group.current.scale.x, s, k);
      group.current.scale.z = THREE.MathUtils.lerp(group.current.scale.z, s, k);
    }
  });

  const mullions = useMemo(() => Array.from({ length: 9 }, (_, i) => -W / 2 + 0.6 + (i * (W - 1.2)) / 8), []);

  return (
    <group
      ref={group}
      position={[0, y, 0]}
      onPointerOver={
        interactive
          ? (e) => {
              e.stopPropagation();
              document.body.style.cursor = 'pointer';
              onOver();
            }
          : undefined
      }
      onPointerOut={
        interactive
          ? () => {
              document.body.style.cursor = '';
              onOut();
            }
          : undefined
      }
      onClick={
        interactive
          ? (e) => {
              e.stopPropagation();
              onClick();
            }
          : undefined
      }
    >
      {/* Перекрытие */}
      <RoundedBox args={[W + 0.35, 0.14, D + 0.35]} radius={0.04} position={[0, 0.07, 0]} castShadow receiveShadow>
        <meshStandardMaterial color={floor.isCommercial ? '#d9d2c5' : '#f1ece3'} roughness={0.85} />
      </RoundedBox>
      {/* Стеклянный пояс */}
      <mesh position={[0, height / 2 + 0.07, 0]} castShadow>
        <boxGeometry args={[W - 0.1, height - 0.14, D - 0.1]} />
        <meshStandardMaterial ref={glass} color="#9fb7c9" metalness={0.65} roughness={0.12} transparent opacity={0.92} />
      </mesh>
      {/* Вертикальные ламели фасада */}
      {mullions.map((x) => (
        <group key={x}>
          <mesh position={[x, height / 2 + 0.07, D / 2 - 0.02]} castShadow>
            <boxGeometry args={[0.08, height - 0.14, 0.12]} />
            <meshStandardMaterial color="#2a2e37" roughness={0.5} />
          </mesh>
          <mesh position={[x, height / 2 + 0.07, -D / 2 + 0.02]}>
            <boxGeometry args={[0.08, height - 0.14, 0.12]} />
            <meshStandardMaterial color="#2a2e37" roughness={0.5} />
          </mesh>
        </group>
      ))}
      {/* Балконы через этаж — чтобы фасад не был плоским */}
      {!floor.isCommercial && floor.number % 2 === 0 && (
        <mesh position={[W / 4, 0.12, D / 2 + 0.45]} castShadow>
          <boxGeometry args={[W / 2.2, 0.08, 0.9]} />
          <meshStandardMaterial color="#e7e1d6" roughness={0.8} />
        </mesh>
      )}
      {!floor.isCommercial && floor.number % 2 === 1 && (
        <mesh position={[-W / 4, 0.12, D / 2 + 0.45]} castShadow>
          <boxGeometry args={[W / 2.2, 0.08, 0.9]} />
          <meshStandardMaterial color="#e7e1d6" roughness={0.8} />
        </mesh>
      )}

      {showLabel && hovered && (
        <Html position={[W / 2 + 0.8, height / 2, 0]} center={false} zIndexRange={[20, 0]} style={{ pointerEvents: 'none' }}>
          <div className="w-48 -translate-y-1/2 rounded-2xl bg-ink/95 px-4 py-3 text-white shadow-lift backdrop-blur">
            <p className="text-[11px] uppercase tracking-[0.2em] text-gold">{floor.isCommercial ? 'Коммерция' : 'Этаж'}</p>
            <p className="font-display text-3xl leading-none">{floor.isCommercial ? '0' : floor.number}</p>
            <p className="mt-2 text-sm text-white/70">
              Свободно <span className="font-semibold text-white">{floor.freeApartments}</span> из {floor.totalApartments}
            </p>
          </div>
        </Html>
      )}
    </group>
  );
}

function Roof({ y }: { y: number }) {
  return (
    <group position={[0, y, 0]}>
      <RoundedBox args={[W + 0.6, 0.22, D + 0.6]} radius={0.06} position={[0, 0.11, 0]} castShadow>
        <meshStandardMaterial color="#1b1e25" roughness={0.6} />
      </RoundedBox>
      <mesh position={[W / 2 - 1.6, 0.6, 0]} castShadow>
        <boxGeometry args={[2.4, 0.8, 2.4]} />
        <meshStandardMaterial color="#2a2e37" roughness={0.7} />
      </mesh>
      {/* Золотой венец — «звезда» Sitora */}
      <mesh position={[0, 0.3, D / 2 + 0.3]}>
        <boxGeometry args={[W + 0.6, 0.06, 0.05]} />
        <meshStandardMaterial color="#c9a46a" emissive="#c9a46a" emissiveIntensity={0.6} metalness={0.8} roughness={0.3} />
      </mesh>
    </group>
  );
}

export default Building3D;
