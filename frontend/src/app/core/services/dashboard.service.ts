import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE } from '../api';
import {
  AttendanceByCell,
  DashboardKpis,
  MonthlyComparison,
  RecentEvent,
  WeeklyTrend
} from '../models/dashboard.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  kpis(from: string, to: string): Observable<DashboardKpis> {
    return this.http.get<DashboardKpis>(`${API_BASE}/dashboard/kpis`, {
      params: this.rangeParams(from, to)
    });
  }

  weeklyTrend(weeks = 12): Observable<WeeklyTrend> {
    return this.http.get<WeeklyTrend>(`${API_BASE}/dashboard/weekly-trend`, {
      params: new HttpParams().set('weeks', weeks)
    });
  }

  attendanceByCell(from: string, to: string): Observable<AttendanceByCell> {
    return this.http.get<AttendanceByCell>(`${API_BASE}/dashboard/attendance-by-cell`, {
      params: this.rangeParams(from, to)
    });
  }

  monthlyComparison(months = 6): Observable<MonthlyComparison> {
    return this.http.get<MonthlyComparison>(`${API_BASE}/dashboard/monthly-comparison`, {
      params: new HttpParams().set('months', months)
    });
  }

  recentEvents(count = 6): Observable<RecentEvent[]> {
    return this.http.get<RecentEvent[]>(`${API_BASE}/dashboard/recent-events`, {
      params: new HttpParams().set('count', count)
    });
  }

  private rangeParams(from: string, to: string): HttpParams {
    return new HttpParams().set('from', from).set('to', to);
  }
}
