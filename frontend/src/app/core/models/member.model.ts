import { Role } from './auth.model';

export interface Member {
  id: string;
  lastName: string;
  firstName: string;
  fullName: string;
  email: string;
  phoneNo: string | null;
  cellId: string | null;
  cellName: string | null;
  roles: Role[];
  hasLogin: boolean;
  isActive: boolean;
  createdAt: string;
}

export interface CreateLogin {
  username: string;
  password: string;
}

export interface MemberPayload {
  lastName: string;
  firstName: string;
  email: string;
  phoneNo: string | null;
  roles: Role[];
  cellId: string | null;
  createLogin?: CreateLogin;
}

export interface MemberQuery {
  search?: string;
  role?: Role | '';
  cellId?: string | null;
  page: number;
  pageSize: number;
  includeInactive?: boolean;
  unassigned?: boolean;
}

export interface MemberQrCode {
  memberId: string;
  memberName: string;
  payload: string;
}
