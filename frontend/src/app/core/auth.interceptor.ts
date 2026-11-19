import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './services/auth.service';

const RETRIED = new HttpContextToken<boolean>(() => false);

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const isAuthRequest = request.url.includes('/auth/login') || request.url.includes('/auth/refresh');
  const token = auth.accessToken();

  const authorizedRequest =
    token && !isAuthRequest
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request;

  return next(authorizedRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 403 && error.error?.code === 'password_change_required') {
        void router.navigate(['/change-password']);
        return throwError(() => error);
      }

      if (error.status === 401 && !isAuthRequest && !request.context.get(RETRIED)) {
        return auth.refreshAccessToken().pipe(
          switchMap(() => {
            const refreshed = auth.accessToken();
            return next(
              request.clone({
                context: request.context.set(RETRIED, true),
                setHeaders: refreshed ? { Authorization: `Bearer ${refreshed}` } : {}
              })
            );
          }),
          catchError((refreshError) => {
            auth.clearSession();
            void router.navigate(['/login'], {
              queryParams: { returnUrl: router.url }
            });
            return throwError(() => refreshError);
          })
        );
      }

      return throwError(() => error);
    })
  );
};
