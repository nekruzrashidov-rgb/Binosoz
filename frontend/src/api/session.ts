import { useSyncExternalStore } from 'react';
import type { AuthResponse, CurrentUser } from './types';

// Две независимые сессии: сотрудник (CRM) и покупатель (кабинет). На одном компьютере
// менеджер может проверять кабинет клиента, не выходя из CRM.
export type Realm = 'staff' | 'customer';

export interface Session {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  user: CurrentUser;
}

const key = (realm: Realm) => `sitora.session.${realm}`;
const listeners = new Set<() => void>();
const cache: Partial<Record<Realm, Session | null>> = {};

function read(realm: Realm): Session | null {
  if (realm in cache) return cache[realm] ?? null;
  let value: Session | null = null;
  try {
    const raw = localStorage.getItem(key(realm));
    value = raw ? (JSON.parse(raw) as Session) : null;
  } catch {
    value = null;
  }
  cache[realm] = value;
  return value;
}

export function getSession(realm: Realm): Session | null {
  return read(realm);
}

export function setSession(realm: Realm, auth: AuthResponse | null) {
  const value: Session | null = auth
    ? {
        accessToken: auth.accessToken,
        accessTokenExpiresAt: auth.accessTokenExpiresAt,
        refreshToken: auth.refreshToken,
        user: auth.user,
      }
    : null;
  cache[realm] = value;
  try {
    if (value) localStorage.setItem(key(realm), JSON.stringify(value));
    else localStorage.removeItem(key(realm));
  } catch {
    // Хранилище недоступно (приватный режим) — сессия живёт до перезагрузки страницы.
  }
  listeners.forEach((l) => l());
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  const onStorage = (e: StorageEvent) => {
    if (e.key?.startsWith('sitora.session.')) {
      delete cache.staff;
      delete cache.customer;
      listener();
    }
  };
  window.addEventListener('storage', onStorage);
  return () => {
    listeners.delete(listener);
    window.removeEventListener('storage', onStorage);
  };
}

export function useSession(realm: Realm): Session | null {
  return useSyncExternalStore(subscribe, () => read(realm));
}

// Язык данных каталога (название и описание дома) — уходит в Accept-Language.
export type Lang = 'ru' | 'tg';
let lang: Lang = (() => {
  try {
    return (localStorage.getItem('sitora.lang') as Lang) || 'ru';
  } catch {
    return 'ru';
  }
})();
const langListeners = new Set<() => void>();

export const getLang = () => lang;
export function setLang(value: Lang) {
  lang = value;
  try {
    localStorage.setItem('sitora.lang', value);
  } catch {
    /* не критично */
  }
  langListeners.forEach((l) => l());
}
export function useLang(): Lang {
  return useSyncExternalStore(
    (l) => {
      langListeners.add(l);
      return () => langListeners.delete(l);
    },
    () => lang,
  );
}
