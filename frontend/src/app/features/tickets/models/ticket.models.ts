// ---- Ticket enums (mirror Daraban.Modules.ServiceDesk.Data.Entities) ----

export enum TicketType {
  Incident = 1,
  Request = 2,
  Problem = 3,
  Change = 4,
}

export enum TicketStatus {
  New = 1,
  Assigned = 2,
  InProgress = 3,
  WaitingForUser = 4,
  WaitingForSupplier = 5,
  Solved = 6,
  Closed = 7,
  Cancelled = 8,
}

export enum TicketPriority {
  Low = 1,
  Medium = 2,
  High = 3,
  VeryHigh = 4,
  Critical = 5,
}

export enum TicketImpact {
  Low = 1,
  Medium = 2,
  High = 3,
}

export enum TicketUrgency {
  Low = 1,
  Medium = 2,
  High = 3,
}

export enum TicketSource {
  Helpdesk = 1,
  Phone = 2,
  Email = 3,
  Web = 4,
  Sms = 5,
  Api = 6,
}

export enum TicketValidationStatus {
  None = 1,
  WaitingApproval = 2,
  Approved = 3,
  Rejected = 4,
}

export enum TicketTaskType {
  Comment = 1,
  Action = 2,
  StatusChange = 3,
  Assignment = 4,
}

// ---- Ticket interfaces ----

export interface Ticket {
  id: string;
  entityId: string;
  type: TicketType;
  status: TicketStatus;
  priority: TicketPriority;
  impact: TicketImpact;
  urgency: TicketUrgency;
  calculatedScore?: number | null;
  title: string;
  description?: string | null;
  solution?: string | null;
  openedAt: string;
  lastUpdated?: string | null;
  closedAt?: string | null;
  solvedAt?: string | null;
  dueDate?: string | null;
  escalationLevel: number;
  isEscalated: boolean;
  requesterUserId: string;
  assignedUserId?: string | null;
  assignedGroupId?: string | null;
  itilCategoryId?: string | null;
  slaLevelId?: string | null;
  assetId?: string | null;
  locationId?: string | null;
  source: TicketSource;
  validationStatus: TicketValidationStatus;
  satisfactionRating?: number | null;
  satisfactionComment?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface TicketListItem {
  id: string;
  type: TicketType;
  status: TicketStatus;
  priority: TicketPriority;
  title: string;
  requesterUserId: string;
  assignedUserId?: string | null;
  openedAt: string;
  dueDate?: string | null;
  isEscalated: boolean;
}

export interface TicketPagedResult {
  items: TicketListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CreateTicketRequest {
  type: TicketType;
  priority: TicketPriority;
  impact: TicketImpact;
  urgency: TicketUrgency;
  title: string;
  description?: string | null;
  requesterUserId: string;
  assignedUserId?: string | null;
  assignedGroupId?: string | null;
  itilCategoryId?: string | null;
  slaLevelId?: string | null;
  assetId?: string | null;
  locationId?: string | null;
  source: TicketSource;
}

export interface UpdateTicketRequest {
  type: TicketType;
  priority: TicketPriority;
  impact: TicketImpact;
  urgency: TicketUrgency;
  title: string;
  description?: string | null;
  assignedUserId?: string | null;
  assignedGroupId?: string | null;
  itilCategoryId?: string | null;
  slaLevelId?: string | null;
  assetId?: string | null;
  locationId?: string | null;
}

export interface TicketTask {
  id: string;
  ticketId: string;
  userId: string;
  content: string;
  type: TicketTaskType;
  previousStatus?: TicketStatus | null;
  newStatus?: TicketStatus | null;
  timeSpentMinutes?: number | null;
  isPrivate: boolean;
  createdAt: string;
}

export interface CreateTicketTaskRequest {
  content: string;
  type: TicketTaskType;
  timeSpentMinutes?: number | null;
  isPrivate: boolean;
}

export interface TicketTemplate {
  id: string;
  name: string;
  description?: string | null;
  defaultType: TicketType;
  defaultPriority: TicketPriority;
  defaultImpact: TicketImpact;
  defaultUrgency: TicketUrgency;
  titleTemplate?: string | null;
  descriptionTemplate?: string | null;
  defaultCategoryId?: string | null;
  defaultAssignedUserId?: string | null;
  defaultAssignedGroupId?: string | null;
  isActive: boolean;
  sortOrder: number;
}

export interface TicketHistoryEntry {
  id: string;
  ticketId: string;
  userId: string;
  fieldName: string;
  oldValue?: string | null;
  newValue?: string | null;
  action: number;
  occurredAt: string;
  comment?: string | null;
}

// ---- Label options (for selects / badges) ----

export const TICKET_TYPE_OPTIONS = [
  { value: TicketType.Incident, label: 'Incident' },
  { value: TicketType.Request, label: 'Request' },
  { value: TicketType.Problem, label: 'Problem' },
  { value: TicketType.Change, label: 'Change' },
];

export const TICKET_STATUS_OPTIONS = [
  { value: TicketStatus.New, label: 'New' },
  { value: TicketStatus.Assigned, label: 'Assigned' },
  { value: TicketStatus.InProgress, label: 'In Progress' },
  { value: TicketStatus.WaitingForUser, label: 'Waiting for User' },
  { value: TicketStatus.WaitingForSupplier, label: 'Waiting for Supplier' },
  { value: TicketStatus.Solved, label: 'Solved' },
  { value: TicketStatus.Closed, label: 'Closed' },
  { value: TicketStatus.Cancelled, label: 'Cancelled' },
];

export const TICKET_PRIORITY_OPTIONS = [
  { value: TicketPriority.Low, label: 'Low' },
  { value: TicketPriority.Medium, label: 'Medium' },
  { value: TicketPriority.High, label: 'High' },
  { value: TicketPriority.VeryHigh, label: 'Very High' },
  { value: TicketPriority.Critical, label: 'Critical' },
];

export const TICKET_IMPACT_OPTIONS = [
  { value: TicketImpact.Low, label: 'Low' },
  { value: TicketImpact.Medium, label: 'Medium' },
  { value: TicketImpact.High, label: 'High' },
];

export const TICKET_URGENCY_OPTIONS = [
  { value: TicketUrgency.Low, label: 'Low' },
  { value: TicketUrgency.Medium, label: 'Medium' },
  { value: TicketUrgency.High, label: 'High' },
];

export const TICKET_SOURCE_OPTIONS = [
  { value: TicketSource.Helpdesk, label: 'Helpdesk' },
  { value: TicketSource.Phone, label: 'Phone' },
  { value: TicketSource.Email, label: 'Email' },
  { value: TicketSource.Web, label: 'Web' },
  { value: TicketSource.Sms, label: 'SMS' },
  { value: TicketSource.Api, label: 'API' },
];

export const TICKET_VALIDATION_LABELS: Record<number, string> = {
  1: 'None',
  2: 'Waiting Approval',
  3: 'Approved',
  4: 'Rejected',
};

// ---- Knowledge Base models (mirror Daraban.Modules.Knowledge DTOs) ----

export interface KbCategory {
  id: string;
  parentId?: string | null;
  name: string;
  slug: string;
  description?: string | null;
  sortOrder: number;
  isActive: boolean;
  articleCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface KbArticle {
  id: string;
  title: string;
  content: string;
  summary?: string | null;
  categoryId?: string | null;
  categoryName?: string | null;
  status: number;
  isFaq: boolean;
  authorUserId: string;
  publishedAt?: string | null;
  viewCount: number;
  helpfulCount: number;
  notHelpfulCount: number;
  tags?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface KbArticleListItem {
  id: string;
  title: string;
  summary?: string | null;
  categoryId?: string | null;
  status: number;
  isFaq: boolean;
  authorUserId: string;
  viewCount: number;
  helpfulCount: number;
  notHelpfulCount: number;
  updatedAt: string;
}

export interface KbArticleSearchHit {
  article: KbArticleListItem;
  rank: number;
}

export interface KbArticleSearchResult {
  items: KbArticleSearchHit[];
  totalCount: number;
  page: number;
  pageSize: number;
  query: string;
}

export interface KbFeedbackSummary {
  articleId: string;
  helpfulCount: number;
  notHelpfulCount: number;
}
