import { Role } from './auth.model';

export interface AttendanceMember {
  id: string;
  fullName: string;
  email: string;
  cellName: string | null;
  roles: Role[];
  checkedInAt: string | null;
}

export interface AttendanceDay {
  eventId: string;
  eventName: string;
  dayId: string;
  eventDate: string;
  attendees: AttendanceMember[];
  nonAttendees: AttendanceMember[];
  activeMemberCount: number;
  checkedInCount: number;
  attendanceRatePercent: number;
}

export interface CheckInResult {
  added: number;
  alreadyCheckedIn: number;
  checkedInCount: number;
}

export interface ScanCheckInResult {
  memberId: string;
  memberName: string;
  alreadyCheckedIn: boolean;
  checkedInCount: number;
}
