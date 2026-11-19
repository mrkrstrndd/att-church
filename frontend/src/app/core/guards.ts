import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './services/auth.service';
import { Role } from './models/auth.model';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login'], {
      queryParams: state.url && state.url !== '/' ? { returnUrl: state.url } : {}
    });
  }

  if (auth.user()?.mustChangePassword && !state.url.startsWith('/change-password')) {
    return router.createUrlTree(['/change-password']);
  }

  return true;
};

export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const roles = (route.data['roles'] as string[] | undefined) ?? [];

  if (roles.length === 0 || roles.some((role) => auth.hasRole(role as Role))) {
    return true;
  }

  return router.createUrlTree(['/events']);
};
