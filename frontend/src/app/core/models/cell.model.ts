import { Role } from './auth.model';

export interface CellMember {
  id: string;
  fullName: string;
  email: string;
  phoneNo: string | null;
  roles: Role[];
  isLeader: boolean;
}

export interface CellListItem {
  id: string;
  name: string;
  description: string | null;
  leaderMemberId: string;
  leaderName: string;
  memberCount: number;
  createdAt: string;
}

export interface CellDetail {
  id: string;
  name: string;
  description: string | null;
  leaderMemberId: string;
  leaderName: string;
  members: CellMember[];
  createdAt: string;
}

export interface CellPayload {
  name: string;
  description: string | null;
  leaderMemberId: string;
}

export interface AddCellMembersPayload {
  memberIds: string[];
}
