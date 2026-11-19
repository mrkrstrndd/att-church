import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE, PagedResult } from '../api';
import { Member, MemberPayload, MemberQrCode, MemberQuery, VipLookup, InviterOption } from '../models/member.model';

@Injectable({ providedIn: 'root' })
export class MemberService {
  private readonly http = inject(HttpClient);

  list(query: MemberQuery): Observable<PagedResult<Member>> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize);

    if (query.search?.trim()) {
      params = params.set('search', query.search.trim());
    }
    if (query.role) {
      params = params.set('role', query.role);
    }
    if (query.cellId) {
      params = params.set('cellId', query.cellId);
    }
    if (query.includeInactive) {
      params = params.set('includeInactive', true);
    }
    if (query.unassigned) {
      params = params.set('unassigned', true);
    }

    return this.http.get<PagedResult<Member>>(`${API_BASE}/members`, { params });
  }

  get(id: string): Observable<Member> {
    return this.http.get<Member>(`${API_BASE}/members/${id}`);
  }

  create(payload: MemberPayload): Observable<Member> {
    return this.http.post<Member>(`${API_BASE}/members`, payload);
  }

  update(id: string, payload: MemberPayload): Observable<Member> {
    return this.http.put<Member>(`${API_BASE}/members/${id}`, payload);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${API_BASE}/members/${id}`);
  }

  getQrCode(id: string): Observable<MemberQrCode> {
    return this.http.get<MemberQrCode>(`${API_BASE}/members/${id}/qr-code`);
  }

  rotateQrCode(id: string): Observable<MemberQrCode> {
    return this.http.post<MemberQrCode>(`${API_BASE}/members/${id}/qr-code/rotate`, {});
  }

  lookupVips(lastName: string): Observable<VipLookup[]> {
    return this.http.get<VipLookup[]>(`${API_BASE}/members/vip-lookup`, {
      params: new HttpParams().set('lastName', lastName)
    });
  }

  inviterOptions(search = ''): Observable<InviterOption[]> {
    let params = new HttpParams();
    if (search.trim()) {
      params = params.set('search', search.trim());
    }
    return this.http.get<InviterOption[]>(`${API_BASE}/members/inviter-options`, { params });
  }
}
