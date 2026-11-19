export type Role = 'Member' | 'CellLeader' | 'Admin';

export interface UserProfile {
  id: string;
  memberId: string;
  username: string;
  displayName: string;
  firstName: string;
  lastName: string;
  email: string;
  roles: Role[];
  cellId: string | null;
  cellName: string | null;
  mustChangePassword: boolean;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: UserProfile;
}

export interface LoginRequest {
  username: string;
  password: string;
  rememberMe: boolean;
}
