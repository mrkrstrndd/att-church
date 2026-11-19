import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE } from '../api';
import { CellDetail, CellListItem, CellPayload } from '../models/cell.model';

@Injectable({ providedIn: 'root' })
export class CellService {
  private readonly http = inject(HttpClient);

  list(): Observable<CellListItem[]> {
    return this.http.get<CellListItem[]>(`${API_BASE}/cells`);
  }

  get(id: string): Observable<CellDetail> {
    return this.http.get<CellDetail>(`${API_BASE}/cells/${id}`);
  }

  create(payload: CellPayload): Observable<CellDetail> {
    return this.http.post<CellDetail>(`${API_BASE}/cells`, payload);
  }

  update(id: string, payload: CellPayload): Observable<CellDetail> {
    return this.http.put<CellDetail>(`${API_BASE}/cells/${id}`, payload);
  }

  addMembers(id: string, memberIds: string[]): Observable<void> {
    return this.http.post<void>(`${API_BASE}/cells/${id}/members`, { memberIds });
  }

  removeMember(id: string, memberId: string): Observable<void> {
    return this.http.delete<void>(`${API_BASE}/cells/${id}/members/${memberId}`);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${API_BASE}/cells/${id}`);
  }
}
