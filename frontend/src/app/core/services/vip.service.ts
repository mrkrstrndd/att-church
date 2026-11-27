import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE } from '../api';
import { VipReport } from '../models/vip.model';

@Injectable({ providedIn: 'root' })
export class VipService {
  private readonly http = inject(HttpClient);

  report(): Observable<VipReport> {
    return this.http.get<VipReport>(`${API_BASE}/vip/report`);
  }
}
