import { api } from './client';
import type { Realm } from './session';
import type * as T from './types';

const S: Realm = 'staff';
const C: Realm = 'customer';
const ALL = { pageSize: 100 };

// --- Публичный каталог ---
export const catalog = {
  buildings: () => api<T.PagedResult<T.Building>>('buildings', { query: ALL }),
  building: (id: string) => api<T.Building>(`buildings/${id}`),
  floors: (buildingId: string) => api<T.PagedResult<T.Floor>>(`buildings/${buildingId}/floors`, { query: ALL }),
  apartments: (floorId: string) => api<T.PagedResult<T.Apartment>>(`floors/${floorId}/apartments`, { query: ALL }),
  apartment: (id: string) => api<T.Apartment>(`apartments/${id}`),
  rate: () => api<T.RateResponse>('rates/current'),
  createLead: (body: T.CreateLeadRequest) => api<T.LeadResponse>('leads', { method: 'POST', body }),
  book: (apartmentId: string, body: T.CreateBookingRequest, realm?: Realm) =>
    api<T.BookingResponse>(`apartments/${apartmentId}/bookings`, { method: 'POST', body, realm }),
};

// --- Вход ---
export const auth = {
  login: (email: string, password: string) =>
    api<T.AuthResponse>('auth/login', { method: 'POST', body: { email, password } }),
  smsSend: (phone: string) => api<T.SmsSendResponse>('auth/sms/send', { method: 'POST', body: { phone } }),
  smsVerify: (phone: string, code: string) =>
    api<T.AuthResponse>('auth/sms/verify', { method: 'POST', body: { phone, code } }),
  logout: (refreshToken: string) => api<void>('auth/logout', { method: 'POST', body: { refreshToken } }),
  me: (realm: Realm) => api<T.CurrentUser>('auth/me', { realm }),
};

// --- Общие для кабинета и CRM: сделка, график, документы, чат, стройка ---
export const dealData = (realm: Realm) => ({
  schedule: (dealId: string) => api<T.ScheduleResponse>(`deals/${dealId}/schedule`, { realm }),
  documents: (dealId: string) => api<T.DocumentResponse[]>(`deals/${dealId}/documents`, { realm }),
  documentLink: (id: string) => api<T.FileLink>(`documents/${id}/download`, { realm }),
  messages: (dealId: string, page = 1) =>
    api<T.PagedResult<T.ChatMessage>>(`deals/${dealId}/messages`, { realm, query: { page, pageSize: 50 } }),
  sendMessage: (dealId: string, text: string) =>
    api<T.ChatMessage>(`deals/${dealId}/messages`, { realm, method: 'POST', body: { text } }),
  stages: (buildingId: string) => api<T.BuildingProgress>(`buildings/${buildingId}/stages`, { realm }),
});

// --- Кабинет покупателя ---
export const cabinet = {
  deals: () => api<T.DealResponse[]>('me/deals', { realm: C }),
  ...dealData(C),
};

// --- CRM ---
export const crm = {
  ...dealData(S),
  summary: () => api<T.CrmSummary>('crm/summary', { realm: S }),
  pipeline: () => api<{ columns: T.PipelineColumn[] }>('crm/pipeline', { realm: S }),

  leads: (status?: T.LeadStatus, page = 1) =>
    api<T.PagedResult<T.LeadResponse>>('leads', { realm: S, query: { status, page, pageSize: 20 } }),
  updateLead: (id: string, body: { status?: T.LeadStatus | null; managerId?: string | null; comment?: string | null }) =>
    api<T.LeadResponse>(`leads/${id}`, { realm: S, method: 'PATCH', body }),
  createLead: (body: T.CreateLeadRequest) => api<T.LeadResponse>('leads', { realm: S, method: 'POST', body }),

  book: (apartmentId: string, body: T.CreateBookingRequest) => catalog.book(apartmentId, body, S),
  extendBooking: (id: string) => api<T.BookingResponse>(`bookings/${id}/extend`, { realm: S, method: 'POST' }),
  cancelBooking: (id: string, reason: string) =>
    api<T.BookingResponse>(`bookings/${id}/cancel`, { realm: S, method: 'POST', body: { reason } }),

  customers: (search?: string, page = 1) =>
    api<T.PagedResult<T.CustomerListItem>>('customers', { realm: S, query: { search, page, pageSize: 20 } }),
  customer: (id: string) => api<T.CustomerResponse>(`customers/${id}`, { realm: S }),
  createCustomer: (body: T.CreateCustomerRequest) =>
    api<T.CustomerResponse>('customers', { realm: S, method: 'POST', body }),

  deals: (query: { stage?: T.DealStage; customerId?: string; page?: number }) =>
    api<T.PagedResult<T.DealResponse>>('deals', { realm: S, query: { ...query, pageSize: 20 } }),
  deal: (id: string) => api<T.DealResponse>(`deals/${id}`, { realm: S }),
  createDeal: (body: T.CreateDealRequest) => api<T.DealResponse>('deals', { realm: S, method: 'POST', body }),
  changeStage: (id: string, stage: T.DealStage, reason?: string) =>
    api<T.DealResponse>(`deals/${id}/stage`, { realm: S, method: 'PATCH', body: { stage, reason: reason || null } }),

  createPlan: (dealId: string, body: T.CreatePlanRequest) =>
    api<T.ScheduleResponse>(`deals/${dealId}/plan`, { realm: S, method: 'POST', body }),
  addPayment: (dealId: string, body: T.CreatePaymentRequest) =>
    api<T.PaymentResponse>(`deals/${dealId}/payments`, { realm: S, method: 'POST', body }),
  reversePayment: (id: string, reason: string) =>
    api<T.PaymentResponse>(`payments/${id}/reverse`, { realm: S, method: 'POST', body: { reason } }),
  uploadDocument: (dealId: string, form: FormData) =>
    api<T.DocumentResponse>(`deals/${dealId}/documents`, { realm: S, method: 'POST', body: form }),

  rate: () => api<T.RateResponse>('rates/current', { realm: S }),
  setRate: (date: string, rateToTjs: number) =>
    api<T.RateResponse>('rates', { realm: S, method: 'PUT', body: { date, currency: 'USD', rateToTjs } }),

  paymentsReport: (from?: string, to?: string) => api<T.PaymentsReport>('reports/payments', { realm: S, query: { from, to } }),
  debtsReport: (asOf?: string) => api<T.DebtsReport>('reports/debts', { realm: S, query: { asOf } }),

  saveBuilding: (body: T.SaveBuildingRequest, id?: string) =>
    api<T.Building>(id ? `buildings/${id}` : 'buildings', { realm: S, method: id ? 'PUT' : 'POST', body }),
  createFloor: (buildingId: string, number: number, planImageUrl: string | null) =>
    api<T.Floor>('floors', { realm: S, method: 'POST', body: { buildingId, number, planImageUrl } }),
  updateFloor: (id: string, number: number, planImageUrl: string | null) =>
    api<T.Floor>(`floors/${id}`, { realm: S, method: 'PUT', body: { number, planImageUrl } }),
  saveApartment: (body: T.SaveApartmentRequest, id?: string) =>
    api<T.Apartment>(id ? `apartments/${id}` : 'apartments', { realm: S, method: id ? 'PUT' : 'POST', body }),
  setOnRequest: (id: string, onRequest: boolean) =>
    api<T.Apartment>(`apartments/${id}/on-request`, { realm: S, method: 'PATCH', body: { onRequest } }),
  importApartments: (buildingId: string, form: FormData) =>
    api<T.ImportResult>('apartments/import', { realm: S, method: 'POST', body: form, query: { buildingId } }),

  createStage: (buildingId: string, body: { name: string; percent: number; sortOrder: number }) =>
    api<T.Stage>(`buildings/${buildingId}/stages`, { realm: S, method: 'POST', body }),
  updateStage: (id: string, body: { name: string; percent: number; sortOrder: number }) =>
    api<T.Stage>(`stages/${id}`, { realm: S, method: 'PUT', body }),
  uploadPhoto: (stageId: string, form: FormData) =>
    api<T.Photo>(`stages/${stageId}/photos`, { realm: S, method: 'POST', body: form }),

  audit: (query: { entity?: string; from?: string; to?: string; page?: number }) =>
    api<T.PagedResult<T.AuditLog>>('audit', { realm: S, query: { ...query, pageSize: 30 } }),

  users: (page = 1) => api<T.PagedResult<T.UserResponse>>('users', { realm: S, query: { page, pageSize: 50 } }),
  createUser: (body: T.CreateUserRequest) => api<T.UserResponse>('users', { realm: S, method: 'POST', body }),
  setUserStatus: (id: string, isActive: boolean) =>
    api<T.UserResponse>(`users/${id}/status`, { realm: S, method: 'PATCH', body: { isActive } }),
};
