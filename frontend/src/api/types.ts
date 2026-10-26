// Типы ответов и запросов API — один в один с DTO из Sitora.Domain.
// Даты приходят строками ISO, DateOnly — строкой YYYY-MM-DD, перечисления — строками.

export type Guid = string;
export type IsoDate = string;
export type DateOnly = string;

export type Currency = 'TJS' | 'USD';
export type ApartmentStatus = 'Free' | 'Booked' | 'Sold' | 'OnRequest';
export type LeadStatus = 'New' | 'InProgress' | 'Converted' | 'Rejected';
export type LeadSource = 'Website' | 'Phone' | 'Office' | 'Social' | 'Other';
export type PaymentMethod = 'Cash' | 'BankTransfer' | 'Card' | 'Other';
export type ScheduleItemKind = 'DownPayment' | 'Monthly';
export type ScheduleItemStatus = 'Pending' | 'PartiallyPaid' | 'Paid';
export type RateSource = 'Manual' | 'Nbt';
export type DocumentType = 'Contract' | 'Annex' | 'Act' | 'Invoice' | 'Receipt' | 'Other';
export type BookingStatus = 'Active' | 'Expired' | 'Cancelled' | 'Converted';
export type DealStage = 'Lead' | 'Showing' | 'Booking' | 'Contract' | 'Payment' | 'Completed' | 'Cancelled';
export type Role = 'Admin' | 'Manager' | 'Accountant' | 'Content' | 'Customer';
export type PipelineCardKind = 'Lead' | 'Booking' | 'Deal';

export const DEAL_STAGES: DealStage[] = ['Lead', 'Showing', 'Booking', 'Contract', 'Payment', 'Completed', 'Cancelled'];

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

// --- Auth ---
export interface CurrentUser {
  id: Guid;
  email: string | null;
  phone: string | null;
  fullName: string;
  roles: Role[];
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: IsoDate;
  refreshToken: string;
  refreshTokenExpiresAt: IsoDate;
  user: CurrentUser;
}

export interface SmsSendResponse {
  message: string;
  expiresInSeconds: number;
}

export interface UserResponse {
  id: Guid;
  email: string | null;
  phone: string | null;
  fullName: string;
  roles: Role[];
  isActive: boolean;
  createdAt: IsoDate;
}

export interface CreateUserRequest {
  email: string;
  fullName: string;
  role: Role;
  password: string;
  phone?: string | null;
}

// --- Catalog ---
export interface Building {
  id: Guid;
  name: string;
  nameRu: string;
  nameTg: string;
  address: string;
  floorsCount: number;
  description: string | null;
  descriptionRu: string | null;
  descriptionTg: string | null;
  modelUrl: string | null;
  totalApartments: number;
  freeApartments: number;
}

export interface Floor {
  id: Guid;
  buildingId: Guid;
  number: number;
  isCommercial: boolean;
  planImageUrl: string | null;
  totalApartments: number;
  freeApartments: number;
}

export interface Apartment {
  id: Guid;
  floorId: Guid;
  buildingId: Guid;
  floorNumber: number;
  number: number;
  rooms: number;
  area: number;
  pricePerM2: number;
  price: number;
  currency: Currency;
  status: ApartmentStatus;
  planSlot: number;
  model3dId: string | null;
}

export interface SaveBuildingRequest {
  nameRu: string;
  nameTg: string;
  address: string;
  floorsCount: number;
  descriptionRu: string | null;
  descriptionTg: string | null;
  modelUrl: string | null;
}

export interface SaveApartmentRequest {
  floorId?: Guid;
  number: number;
  rooms: number;
  area: number;
  pricePerM2: number;
  planSlot: number;
  model3dId: string | null;
}

export interface ImportResult {
  created: number;
  updated: number;
  errors: { row: number; message: string }[];
  succeeded: boolean;
}

// --- Sales ---
export interface LeadResponse {
  id: Guid;
  name: string;
  phone: string;
  comment: string | null;
  apartmentId: Guid | null;
  source: LeadSource;
  status: LeadStatus;
  managerId: Guid | null;
  createdAt: IsoDate;
}

export interface CreateLeadRequest {
  name: string;
  phone: string;
  apartmentId?: Guid | null;
  source?: LeadSource | null;
  comment?: string | null;
}

export interface BookingResponse {
  id: Guid;
  apartmentId: Guid;
  apartmentNumber: number;
  customerId: Guid | null;
  leadId: Guid | null;
  expiresAt: IsoDate;
  status: BookingStatus;
  createdAt: IsoDate;
}

export interface CreateBookingRequest {
  name?: string | null;
  phone?: string | null;
  customerId?: Guid | null;
  comment?: string | null;
}

export interface CustomerListItem {
  id: Guid;
  fullName: string;
  phone: string;
  email: string | null;
  createdAt: IsoDate;
}

export interface CustomerResponse extends CustomerListItem {
  passportData: string | null;
  userId: Guid | null;
}

export interface CreateCustomerRequest {
  fullName: string;
  phone: string;
  email?: string | null;
  passportData?: string | null;
  leadId?: Guid | null;
}

export interface DealResponse {
  id: Guid;
  apartmentId: Guid;
  apartmentNumber: number;
  buildingName: string;
  customerId: Guid;
  customerName: string;
  managerId: Guid;
  stage: DealStage;
  stageReason: string | null;
  price: number;
  discount: number;
  discountReason: string | null;
  finalPrice: number;
  currency: Currency;
  bookingId: Guid | null;
  createdAt: IsoDate;
  updatedAt: IsoDate;
}

export interface CreateDealRequest {
  apartmentId: Guid;
  customerId: Guid;
  stage?: DealStage | null;
  currency?: Currency | null;
  discount?: number | null;
  discountReason?: string | null;
}

export interface MoneyTotal {
  currency: Currency;
  amount: number;
}

export interface CrmSummary {
  newLeads: number;
  leadsInProgress: number;
  activeBookings: number;
  bookingsExpiringIn24Hours: number;
  dealsByStage: Partial<Record<DealStage, number>>;
  apartmentsByStatus: Partial<Record<ApartmentStatus, number>>;
  soldAmount: MoneyTotal[];
}

export interface PipelineCard {
  kind: PipelineCardKind;
  id: Guid;
  clientName: string;
  phone: string;
  apartmentId: Guid | null;
  apartmentNumber: number | null;
  amount: number | null;
  currency: Currency | null;
  createdAt: IsoDate;
  expiresAt: IsoDate | null;
}

export interface PipelineColumn {
  stage: DealStage;
  count: number;
  cards: PipelineCard[];
}

// --- Payments ---
export interface ScheduleItem {
  id: Guid;
  kind: ScheduleItemKind;
  dueDate: DateOnly;
  amountDue: number;
  amountPaid: number;
  status: ScheduleItemStatus;
  isOverdue: boolean;
  amountDueEquivalent: number | null;
  amountPaidEquivalent: number | null;
}

export interface ScheduleEquivalent {
  currency: Currency;
  rate: number;
  rateDate: DateOnly;
  isEstimate: boolean;
  note: string;
  total: number;
  paid: number;
  remaining: number;
  overdue: number;
}

export interface PaymentResponse {
  id: Guid;
  dealId: Guid;
  amount: number;
  currency: Currency;
  rateUsed: number | null;
  amountInDealCurrency: number;
  amountTjs: number;
  paidAt: DateOnly;
  method: PaymentMethod;
  comment: string | null;
  reversalOfId: Guid | null;
  isReversed: boolean;
  createdAt: IsoDate;
}

export interface ScheduleResponse {
  dealId: Guid;
  currency: Currency;
  total: number;
  paid: number;
  remaining: number;
  overdue: number;
  downPaymentPct: number;
  months: number;
  paymentDay: number;
  startDate: DateOnly;
  items: ScheduleItem[];
  payments: PaymentResponse[];
  equivalent: ScheduleEquivalent | null;
}

export interface CreatePlanRequest {
  downPaymentPct: number;
  months: number;
  paymentDay: number;
  startDate: DateOnly;
}

export interface CreatePaymentRequest {
  amount: number;
  currency: Currency;
  paidAt: DateOnly;
  method: PaymentMethod;
  comment?: string | null;
}

export interface RateResponse {
  date: DateOnly;
  currency: Currency;
  rateToTjs: number;
  source: RateSource;
}

// --- Reports ---
export interface PaymentReportRow {
  paymentId: Guid;
  paidAt: DateOnly;
  dealId: Guid;
  customerName: string;
  buildingName: string;
  apartmentNumber: number;
  amount: number;
  currency: Currency;
  rateUsed: number | null;
  amountTjs: number;
  method: PaymentMethod;
  isReversal: boolean;
  comment: string | null;
}

export interface PaymentsReport {
  from: DateOnly;
  to: DateOnly;
  rows: PaymentReportRow[];
  totalsByCurrency: MoneyTotal[];
  totalTjs: number;
}

export interface DebtReportRow {
  dealId: Guid;
  customerName: string;
  phone: string;
  buildingName: string;
  apartmentNumber: number;
  currency: Currency;
  overdue: number;
  oldestOverdueDate: DateOnly;
  daysOverdue: number;
  remaining: number;
}

export interface DebtsReport {
  asOf: DateOnly;
  rows: DebtReportRow[];
  totalsByCurrency: MoneyTotal[];
}

// --- Documents, chat, construction, audit ---
export interface DocumentResponse {
  id: Guid;
  dealId: Guid;
  type: DocumentType;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  signedAt: IsoDate | null;
  createdAt: IsoDate;
}

export interface FileLink {
  url: string;
  expiresAt: IsoDate;
}

export interface ChatMessage {
  id: Guid;
  dealId: Guid;
  senderId: Guid;
  senderName: string;
  fromCustomer: boolean;
  text: string;
  createdAt: IsoDate;
  readAt: IsoDate | null;
}

export interface Photo {
  id: Guid;
  stageId: Guid;
  url: string;
  caption: string | null;
  takenAt: IsoDate;
}

export interface Stage {
  id: Guid;
  buildingId: Guid;
  name: string;
  percent: number;
  sortOrder: number;
  updatedAt: IsoDate;
  photos: Photo[];
}

export interface BuildingProgress {
  buildingId: Guid;
  overallPercent: number;
  stages: Stage[];
}

export interface AuditLog {
  id: Guid;
  userId: Guid | null;
  userEmail: string | null;
  entity: string;
  entityId: Guid;
  action: string;
  before: string | null;
  after: string | null;
  at: IsoDate;
}
