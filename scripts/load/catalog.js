// Нагрузка на публичный каталог (k6): 100 виртуальных пользователей, 1 минута, порог p95 < 500 мс.
// ТОЛЬКО локально или на staging — не на прод Render (бесплатный тариф, и это нагрузка на чужую инфраструктуру).
//
//   docker compose up -d                       # PostgreSQL, Redis, MinIO
//   ASPNETCORE_ENVIRONMENT=Development dotnet run -c Release --project backend/src/Sitora.WebApi --urls http://localhost:5194
//   k6 run scripts/load/catalog.js                                    # или
//   k6 run -e API_URL=https://staging.example.tj -e ORG=nekruz scripts/load/catalog.js
//
// Сценарий повторяет путь посетителя: список домов → дом → этажи → квартиры случайного этажа → карточка квартиры.
import http from 'k6/http';
import { check, sleep } from 'k6';

const API = (__ENV.API_URL || 'http://localhost:5194') + '/api/v1';
const ORG = __ENV.ORG || 'nekruz';

export const options = {
  vus: 100,
  duration: '1m',
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
  },
};

const params = { headers: { 'Accept-Language': 'ru' } };

export default function () {
  const list = http.get(`${API}/buildings?organization=${ORG}`, params);
  check(list, { 'список домов 200': (r) => r.status === 200 });
  const buildings = list.json('items') || [];
  if (!buildings.length) { sleep(1); return; }
  const building = buildings[Math.floor(Math.random() * buildings.length)];

  check(http.get(`${API}/buildings/${building.id}`, params), { 'дом 200': (r) => r.status === 200 });
  const floors = http.get(`${API}/buildings/${building.id}/floors`, params).json() || [];
  if (floors.length) {
    const floor = floors[Math.floor(Math.random() * floors.length)];
    const apartments = http.get(`${API}/floors/${floor.id}/apartments?pageSize=100`, params).json('items') || [];
    if (apartments.length) {
      const apartment = apartments[Math.floor(Math.random() * apartments.length)];
      check(http.get(`${API}/apartments/${apartment.id}`, params), { 'квартира 200': (r) => r.status === 200 });
    }
  }
  sleep(1);
}
