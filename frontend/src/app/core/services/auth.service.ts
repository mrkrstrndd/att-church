import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, finalize, Observable, of, shareReplay, tap, throwError } from 'rxjs';
import { API_BASE } from '../api';
import { AuthResponse, LoginRequest, Role, UserProfile } from '../models/auth.model';

const K_ACCESS = 'ca.access';
const K_REFRESH = 'ca.refresh';
const K_USER = 'ca.user';
const K_REMEMBER = 'ca.remember';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly _accessToken = signal<string | null>(null);
  private readonly _refreshToken = signal<string | null>(null);
  private readonly _user = signal<UserProfile | null>(null);
  private readonly _rememberMe = signal(false);
  private refreshInFlight: Observable<AuthResponse> | null = null;

  readonly accessToken = this._accessToken.asReadonly();
  readonly user = this._user.asReadonly();
  readonly rememberMe = this._rememberMe.asReadonly();
  readonly isAuthenticated = computed(() => !!this._accessToken() && !!this._refreshToken());
  readonly isAdmin = computed(() => this.hasRole('Admin'));
  readonly isCellLeader = computed(() => this.hasRole('CellLeader'));
  readonly canViewDashboard = computed(() => this.isAdmin() || this.isCellLeader());

  constructor() {
    this.restoreSession();
  }

  hasRole(role: Role): boolean {
    return this._user()?.roles.includes(role) ?? false;
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${API_BASE}/auth/login`, request).pipe(
      tap((response) => this.applySession(response, request.rememberMe))
    );
  }

  refreshAccessToken(): Observable<AuthResponse> {
    if (this.refreshInFlight) {
      return this.refreshInFlight;
    }

    const refreshToken = this._refreshToken();
    if (!refreshToken) {
      return throwError(() => new Error('Your session has expired. Please sign in again.'));
    }

    this.refreshInFlight = this.http
      .post<AuthResponse>(`${API_BASE}/auth/refresh`, { refreshToken })
      .pipe(
        tap((response) => this.applySession(response, this._rememberMe())),
        catchError((error) => {
          this.clearSession();
          return throwError(() => error);
        }),
        finalize(() => (this.refreshInFlight = null)),
        shareReplay({ bufferSize: 1, refCount: false })
      );

    return this.refreshInFlight;
  }

  logout(): Observable<unknown> {
    const refreshToken = this._refreshToken();
    const request = refreshToken
      ? this.http.post<void>(`${API_BASE}/auth/logout`, { refreshToken })
      : of(undefined);

    return request.pipe(
      catchError(() => of(undefined)),
      finalize(() => this.clearSession())
    );
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${API_BASE}/auth/change-password`, {
      currentPassword,
      newPassword
    });
  }

  clearSession(): void {
    this.removeFrom(localStorage);
    this.removeFrom(sessionStorage);
    this._accessToken.set(null);
    this._refreshToken.set(null);
    this._user.set(null);
    this._rememberMe.set(false);
  }

  private applySession(response: AuthResponse, rememberMe: boolean): void {
    this.removeFrom(localStorage);
    this.removeFrom(sessionStorage);

    const store = rememberMe ? localStorage : sessionStorage;
    store.setItem(K_ACCESS, response.accessToken);
    store.setItem(K_REFRESH, response.refreshToken);
    store.setItem(K_USER, JSON.stringify(response.user));
    store.setItem(K_REMEMBER, rememberMe ? '1' : '0');

    this._accessToken.set(response.accessToken);
    this._refreshToken.set(response.refreshToken);
    this._user.set(response.user);
    this._rememberMe.set(rememberMe);
  }

  private restoreSession(): void {
    const localUser = localStorage.getItem(K_USER);
    const sessionUser = sessionStorage.getItem(K_USER);
    const remember = !!localUser || localStorage.getItem(K_REMEMBER) === '1';
    const store = localUser || remember ? localStorage : sessionStorage;
    const access = store.getItem(K_ACCESS);
    const refresh = store.getItem(K_REFRESH);
    const rawUser = localUser ?? sessionUser;

    if (!access || !refresh || !rawUser) {
      return;
    }

    try {
      const user = JSON.parse(rawUser) as UserProfile;
      this._accessToken.set(access);
      this._refreshToken.set(refresh);
      this._user.set(user);
      this._rememberMe.set(store === localStorage);
    } catch {
      this.clearSession();
    }
  }

  private removeFrom(store: Storage): void {
    store.removeItem(K_ACCESS);
    store.removeItem(K_REFRESH);
    store.removeItem(K_USER);
    store.removeItem(K_REMEMBER);
  }
}
