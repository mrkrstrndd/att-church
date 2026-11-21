import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE, PagedResult } from '../api';
import { ChurchEvent, EventListItem, EventMonth, EventPayload } from '../models/event.model';

@Injectable({ providedIn: 'root' })
export class EventService {
  private readonly http = inject(HttpClient);

  list(page = 1, pageSize = 20, month = ''): Observable<PagedResult<EventListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (month) {
      params = params.set('month', month);
    }
    return this.http.get<PagedResult<EventListItem>>(`${API_BASE}/events`, { params });
  }

  months(): Observable<EventMonth[]> {
    return this.http.get<EventMonth[]>(`${API_BASE}/events/months`);
  }

  get(id: string): Observable<ChurchEvent> {
    return this.http.get<ChurchEvent>(`${API_BASE}/events/${id}`);
  }

  create(payload: EventPayload): Observable<ChurchEvent> {
    return this.http.post<ChurchEvent>(`${API_BASE}/events`, payload);
  }

  update(id: string, payload: EventPayload): Observable<ChurchEvent> {
    return this.http.put<ChurchEvent>(`${API_BASE}/events/${id}`, payload);
  }

  delete(id: string, force = false): Observable<void> {
    const params = force ? new HttpParams().set('force', true) : undefined;
    return this.http.delete<void>(`${API_BASE}/events/${id}`, { params });
  }
}
