import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthResponse } from '../models/auth.model';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  const response: AuthResponse = {
    accessToken: 'access-token',
    refreshToken: 'refresh-token',
    expiresAt: '2026-11-05T12:00:00Z',
    user: {
      id: 'user-1',
      memberId: 'member-1',
      username: 'admin',
      displayName: 'System Admin',
      firstName: 'System',
      lastName: 'Admin',
      email: 'admin@church.local',
      roles: ['Admin'],
      cellId: null,
      cellName: null,
      mustChangePassword: false
    }
  };

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
    sessionStorage.clear();
  });

  it('stores tokens in sessionStorage when remember me is off', () => {
    service.login({ username: 'admin', password: 'secret', rememberMe: false }).subscribe();

    const request = http.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    request.flush(response);

    expect(sessionStorage.getItem('ca.access')).toBe('access-token');
    expect(sessionStorage.getItem('ca.refresh')).toBe('refresh-token');
    expect(localStorage.getItem('ca.access')).toBeNull();
  });

  it('stores tokens in localStorage when remember me is on', () => {
    service.login({ username: 'admin', password: 'secret', rememberMe: true }).subscribe();

    http.expectOne('/api/auth/login').flush(response);

    expect(localStorage.getItem('ca.access')).toBe('access-token');
    expect(localStorage.getItem('ca.refresh')).toBe('refresh-token');
    expect(sessionStorage.getItem('ca.access')).toBeNull();
  });

  it('rotates the refresh token through the refresh endpoint', () => {
    service.login({ username: 'admin', password: 'secret', rememberMe: true }).subscribe();
    http.expectOne('/api/auth/login').flush(response);

    service.refreshAccessToken().subscribe();
    const refresh = http.expectOne('/api/auth/refresh');
    expect(refresh.request.body.refreshToken).toBe('refresh-token');
    refresh.flush({ ...response, accessToken: 'new-access', refreshToken: 'new-refresh' });

    expect(service.accessToken()).toBe('new-access');
    expect(localStorage.getItem('ca.refresh')).toBe('new-refresh');
  });
});
