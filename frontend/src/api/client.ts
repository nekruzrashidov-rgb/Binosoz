import { getLang, getSession, setSession, type Realm } from './session';
import type { AuthResponse } from './types';

// Ошибка API: бэкенд отвечает Problem Details с текстами на русском.
export class ApiError extends Error {
  readonly status: number;
  readonly fieldErrors: Record<string, string[]>;
  // Тело ответа как есть: например, ImportResult с ошибками по строкам при 422.
  readonly body: unknown;

  constructor(status: number, message: string, fieldErrors: Record<string, string[]> = {}, body?: unknown) {
    super(message);
    this.status = status;
    this.fieldErrors = fieldErrors;
    this.body = body;
  }

  // Первая ошибка по полю (ключи валидатора — в PascalCase или camelCase).
  field(name: string): string | undefined {
    const entry = Object.entries(this.fieldErrors).find(([k]) => k.toLowerCase() === name.toLowerCase());
    return entry?.[1]?.[0];
  }
}

const FALLBACK: Record<number, string> = {
  0: 'Нет связи с сервером. Проверьте интернет и попробуйте ещё раз.',
  400: 'Проверьте введённые данные.',
  401: 'Сессия истекла. Войдите снова.',
  403: 'Недостаточно прав для этого действия.',
  404: 'Не найдено.',
  409: 'Действие невозможно: данные уже изменились.',
  413: 'Файл слишком большой.',
  415: 'Неподдерживаемый формат файла.',
  422: 'Проверьте введённые данные.',
  429: 'Слишком много запросов. Подождите немного и попробуйте снова.',
  500: 'Ошибка на сервере. Попробуйте позже.',
};

async function toError(response: Response): Promise<ApiError> {
  let message = FALLBACK[response.status] ?? FALLBACK[500];
  let fieldErrors: Record<string, string[]> = {};
  let body: unknown;
  try {
    body = await response.json();
    const b = body as { errors?: unknown; detail?: string; title?: string } | null;
    if (b?.errors && typeof b.errors === 'object' && !Array.isArray(b.errors)) {
      fieldErrors = b.errors as Record<string, string[]>;
      const first = Object.values(fieldErrors)[0]?.[0];
      message = b.detail || first || b.title || message;
    } else {
      message = b?.detail || b?.title || message;
    }
  } catch {
    /* тело не JSON */
  }
  return new ApiError(response.status, message, fieldErrors, body);
}

const refreshing: Partial<Record<Realm, Promise<boolean>>> = {};

async function refresh(realm: Realm): Promise<boolean> {
  const session = getSession(realm);
  if (!session) return false;
  refreshing[realm] ??= (async () => {
    try {
      const response = await fetch('/api/v1/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: session.refreshToken }),
      });
      if (!response.ok) {
        setSession(realm, null);
        return false;
      }
      setSession(realm, (await response.json()) as AuthResponse);
      return true;
    } catch {
      return false;
    } finally {
      delete refreshing[realm];
    }
  })();
  return refreshing[realm]!;
}

export interface RequestOptions {
  method?: string;
  body?: unknown;
  query?: Record<string, string | number | boolean | null | undefined>;
  realm?: Realm;
  signal?: AbortSignal;
}

function buildUrl(path: string, query?: RequestOptions['query']) {
  const url = new URL(path.startsWith('/') ? path : `/api/v1/${path}`, window.location.origin);
  if (query) {
    for (const [k, v] of Object.entries(query)) {
      if (v !== undefined && v !== null && v !== '') url.searchParams.set(k, String(v));
    }
  }
  return url.pathname + url.search;
}

async function send(path: string, options: RequestOptions, retry = true): Promise<Response> {
  const { method = 'GET', body, query, realm, signal } = options;
  const headers: Record<string, string> = { 'Accept-Language': getLang() };
  const session = realm ? getSession(realm) : null;
  if (session) headers.Authorization = `Bearer ${session.accessToken}`;

  let payload: BodyInit | undefined;
  if (body instanceof FormData) payload = body;
  else if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
    payload = JSON.stringify(body);
  }

  let response: Response;
  try {
    response = await fetch(buildUrl(path, query), { method, headers, body: payload, signal });
  } catch (e) {
    if ((e as Error).name === 'AbortError') throw e;
    throw new ApiError(0, FALLBACK[0]);
  }

  if (response.status === 401 && realm && session && retry && (await refresh(realm))) {
    return send(path, options, false);
  }
  if (!response.ok) throw await toError(response);
  return response;
}

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await send(path, options);
  if (response.status === 204) return undefined as T;
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

// Скачивание файла, который отдаётся только с токеном (PDF графика, акт, Excel-отчёты).
export async function downloadAuthorized(path: string, realm: Realm, query?: RequestOptions['query'], fallbackName = 'file') {
  const response = await send(path, { realm, query });
  const blob = await response.blob();
  const disposition = response.headers.get('Content-Disposition') ?? '';
  const match = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(disposition);
  const name = match ? decodeURIComponent(match[1] ?? match[2]) : fallbackName;
  saveBlob(blob, name);
}

export function saveBlob(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

// Подписанная ссылка приходит относительной (/api/v1/files?token=…): открываем её как есть.
export function openSignedLink(url: string) {
  window.open(url, '_blank', 'noopener');
}
