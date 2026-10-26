import type {
  ApartmentStatus,
  BookingStatus,
  Currency,
  DealStage,
  DocumentType,
  LeadSource,
  LeadStatus,
  PaymentMethod,
  Role,
  ScheduleItemStatus,
} from '@/api/types';

const number = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 });
const integer = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 });

export const fmtNumber = (v: number) => number.format(v);

export function money(value: number | null | undefined, currency: Currency | null | undefined = 'TJS', exact = false) {
  if (value === null || value === undefined) return '—';
  const text = exact ? number.format(value) : integer.format(value);
  return currency === 'USD' ? `$${text}` : `${text} сом.`;
}

// Короткая запись крупных сумм: 1,2 млн сом.
export function moneyShort(value: number, currency: Currency = 'TJS') {
  const abs = Math.abs(value);
  const short =
    abs >= 1_000_000
      ? `${(value / 1_000_000).toLocaleString('ru-RU', { maximumFractionDigits: 1 })} млн`
      : abs >= 10_000
        ? `${(value / 1000).toLocaleString('ru-RU', { maximumFractionDigits: 0 })} тыс.`
        : integer.format(value);
  return currency === 'USD' ? `$${short}` : `${short} сом.`;
}

export const area = (v: number) => `${number.format(v)} м²`;

const dateFmt = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long', year: 'numeric' });
const dateShortFmt = new Intl.DateTimeFormat('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric' });
const dateTimeFmt = new Intl.DateTimeFormat('ru-RU', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
const timeFmt = new Intl.DateTimeFormat('ru-RU', { hour: '2-digit', minute: '2-digit' });

// DateOnly (YYYY-MM-DD) разбираем как локальную дату, чтобы не сдвинуться на день из-за часового пояса.
function parse(value: string) {
  return /^\d{4}-\d{2}-\d{2}$/.test(value) ? new Date(`${value}T00:00:00`) : new Date(value);
}

export const date = (v: string | null | undefined) => (v ? dateFmt.format(parse(v)) : '—');
export const dateShort = (v: string | null | undefined) => (v ? dateShortFmt.format(parse(v)) : '—');
export const dateTime = (v: string | null | undefined) => (v ? dateTimeFmt.format(parse(v)) : '—');
export const time = (v: string) => timeFmt.format(parse(v));

export function todayIso() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function monthStartIso() {
  return `${todayIso().slice(0, 8)}01`;
}

// «через 5 ч 12 мин», «истекла»
export function timeLeft(until: string) {
  const ms = new Date(until).getTime() - Date.now();
  if (ms <= 0) return 'истекла';
  const h = Math.floor(ms / 3_600_000);
  const m = Math.floor((ms % 3_600_000) / 60_000);
  return h > 0 ? `${h} ч ${m} мин` : `${m} мин`;
}

export function relative(v: string) {
  const diff = (Date.now() - new Date(v).getTime()) / 1000;
  if (diff < 60) return 'только что';
  if (diff < 3600) return `${Math.floor(diff / 60)} мин назад`;
  if (diff < 86400) return `${Math.floor(diff / 3600)} ч назад`;
  if (diff < 86400 * 7) return `${Math.floor(diff / 86400)} дн. назад`;
  return dateShort(v);
}

export const fileSize = (bytes: number) =>
  bytes >= 1_048_576 ? `${(bytes / 1_048_576).toFixed(1)} МБ` : `${Math.max(1, Math.round(bytes / 1024))} КБ`;

// Телефон: принимаем любой ввод, отправляем +992XXXXXXXXX.
export function normalizePhone(input: string) {
  const digits = input.replace(/\D/g, '');
  if (digits.startsWith('992')) return `+${digits.slice(0, 12)}`;
  return `+992${digits.slice(0, 9)}`;
}

export function formatPhone(phone: string) {
  const d = phone.replace(/\D/g, '');
  if (d.length === 12 && d.startsWith('992')) return `+992 ${d.slice(3, 5)} ${d.slice(5, 8)} ${d.slice(8, 10)} ${d.slice(10)}`;
  return phone;
}

export const roomsLabel = (rooms: number) =>
  rooms === 0 ? 'Студия' : `${rooms}-комнатная`;
export const roomsShort = (rooms: number) => (rooms === 0 ? 'Студия' : `${rooms}-комн.`);

export const apartmentStatusLabel: Record<ApartmentStatus, string> = {
  Free: 'Свободна',
  Booked: 'Забронирована',
  Sold: 'Продана',
  OnRequest: 'Под запрос',
};

export const dealStageLabel: Record<DealStage, string> = {
  Lead: 'Заявка',
  Showing: 'Показ',
  Booking: 'Бронь',
  Contract: 'Договор',
  Payment: 'Оплата',
  Completed: 'Завершена',
  Cancelled: 'Отменена',
};

export const leadStatusLabel: Record<LeadStatus, string> = {
  New: 'Новая',
  InProgress: 'В работе',
  Converted: 'Клиент',
  Rejected: 'Отказ',
};

export const leadSourceLabel: Record<LeadSource, string> = {
  Website: 'Сайт',
  Phone: 'Звонок',
  Office: 'Офис',
  Social: 'Соцсети',
  Other: 'Другое',
};

export const paymentMethodLabel: Record<PaymentMethod, string> = {
  Cash: 'Наличные',
  BankTransfer: 'Перевод',
  Card: 'Карта',
  Other: 'Другое',
};

export const scheduleStatusLabel: Record<ScheduleItemStatus, string> = {
  Pending: 'Ожидается',
  PartiallyPaid: 'Частично',
  Paid: 'Оплачено',
};

export const documentTypeLabel: Record<DocumentType, string> = {
  Contract: 'Договор',
  Annex: 'Приложение',
  Act: 'Акт',
  Invoice: 'Счёт',
  Receipt: 'Квитанция',
  Other: 'Другое',
};

export const bookingStatusLabel: Record<BookingStatus, string> = {
  Active: 'Активна',
  Expired: 'Истекла',
  Cancelled: 'Отменена',
  Converted: 'В сделке',
};

export const roleLabel: Record<Role, string> = {
  Admin: 'Администратор',
  Manager: 'Менеджер',
  Accountant: 'Бухгалтер',
  Content: 'Контент',
  Customer: 'Покупатель',
};

export const auditActionLabel: Record<string, string> = {
  Created: 'Создание',
  Updated: 'Изменение',
  StatusChanged: 'Смена статуса',
  StageChanged: 'Смена этапа',
  Reversed: 'Сторно',
  Uploaded: 'Загрузка',
  Imported: 'Импорт',
  Converted: 'Перевод в клиенты',
  Replaced: 'Замена',
  Extended: 'Продление',
  Expired: 'Истечение срока',
  Cancelled: 'Отмена',
  Blocked: 'Блокировка',
  Unblocked: 'Разблокировка',
};
