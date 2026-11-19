export interface EventDay {
  id: string;
  eventDate: string;
  attendanceCount: number;
}

export interface EventListItem {
  id: string;
  name: string;
  description: string | null;
  isMultiDay: boolean;
  startDate: string;
  endDate: string;
  dayCount: number;
  attendanceCount: number;
}

export interface ChurchEvent {
  id: string;
  name: string;
  description: string | null;
  isMultiDay: boolean;
  startDate: string;
  endDate: string;
  days: EventDay[];
  createdAt: string;
}

export interface EventPayload {
  name: string;
  description: string | null;
  isMultiDay: boolean;
  startDate: string;
  endDate: string;
}
