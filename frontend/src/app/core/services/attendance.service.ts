import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE } from '../api';
import { AttendanceDay, CheckInResult, CreateVipCheckInPayload, ScanCheckInResult, VipCheckInResult } from '../models/attendance.model';

@Injectable({ providedIn: 'root' })
export class AttendanceService {
  private readonly http = inject(HttpClient);

  getDay(eventId: string, dayId: string, search = ''): Observable<AttendanceDay> {
    const params = search.trim() ? new HttpParams().set('search', search.trim()) : undefined;
    return this.http.get<AttendanceDay>(
      `${API_BASE}/events/${eventId}/days/${dayId}/attendance`,
      { params }
    );
  }

  checkIn(eventId: string, dayId: string, memberIds: string[]): Observable<CheckInResult> {
    return this.http.post<CheckInResult>(
      `${API_BASE}/events/${eventId}/days/${dayId}/attendance`,
      { memberIds }
    );
  }

  scanQrCode(eventId: string, dayId: string, qrCode: string): Observable<ScanCheckInResult> {
    return this.http.post<ScanCheckInResult>(
      `${API_BASE}/events/${eventId}/days/${dayId}/attendance/scan`,
      { qrCode }
    );
  }

  createVipCheckIn(
    eventId: string,
    dayId: string,
    payload: CreateVipCheckInPayload
  ): Observable<VipCheckInResult> {
    return this.http.post<VipCheckInResult>(
      `${API_BASE}/events/${eventId}/days/${dayId}/attendance/vip`,
      payload
    );
  }

  remove(eventId: string, dayId: string, memberId: string): Observable<void> {
    return this.http.delete<void>(
      `${API_BASE}/events/${eventId}/days/${dayId}/attendance/${memberId}`
    );
  }
}
