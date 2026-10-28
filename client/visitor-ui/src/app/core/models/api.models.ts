export type Role = 'Admin' | 'User';
export type VisitorStatus = 'Scheduled' | 'CheckedIn' | 'Completed' | 'Cancelled';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresAtUtc: string;
  username: string;
  displayName: string;
  role: Role;
}

export interface ApiError {
  status: number;
  title: string;
  detail: string;
  code: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}

export interface Visitor {
  id: number;
  visitorName: string;
  companyName: string;
  mobileNumber: string;
  email: string | null;
  visitDate: string;
  hostId: number;
  hostName: string;
  purposeId: number;
  purposeName: string;
  remarks: string | null;
  status: VisitorStatus;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  rowVersion: string;
}

export interface VisitorRequest {
  visitorName: string;
  companyName: string;
  mobileNumber: string;
  email: string | null;
  visitDate: string;
  hostId: number;
  purposeId: number;
  remarks: string | null;
}

export interface UpdateVisitorRequest extends VisitorRequest {
  status: VisitorStatus;
  rowVersion: string;
}

export interface VisitorListQuery {
  search: string;
  sortBy: string;
  sortDirection: 'asc' | 'desc';
  page: number;
  pageSize: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Host {
  id: number;
  fullName: string;
  department: string;
}

export interface Purpose {
  id: number;
  name: string;
}