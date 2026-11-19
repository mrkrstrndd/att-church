export interface LastEvent {
  name: string;
  date: string;
  attendance: number;
}

export interface DashboardKpis {
  totalActiveMembers: number;
  totalCells: number;
  eventsThisMonth: number;
  lastEvent: LastEvent | null;
  averageAttendance: number;
  attendanceRatePercent: number;
  averageAttendanceDeltaPercent: number | null;
  eventsThisMonthDeltaPercent: number | null;
  from: string;
  to: string;
}

export interface WeeklyPoint {
  weekStart: string;
  label: string;
  totalAttendance: number;
}

export interface WeeklyTrend {
  points: WeeklyPoint[];
}

export interface CellAttendance {
  cellId: string | null;
  name: string;
  attendanceCount: number;
  memberCount: number;
  uniqueAttendees: number;
}

export interface AttendanceByCell {
  from: string;
  to: string;
  cells: CellAttendance[];
}

export interface MonthlyComparison {
  mode: 'months' | 'year';
  labels: string[];
  totalAttendance: number[];
  uniqueAttendees: number[];
  previousYearAttendance: number[] | null;
  previousYearUniqueAttendees: number[] | null;
}

export interface RecentEvent {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  dayCount: number;
  attendanceCount: number;
  averagePerDay: number;
}
