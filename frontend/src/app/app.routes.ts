import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards';
import { ShellComponent } from './features/shell/shell.component';

export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in · Church Attendance',
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'change-password',
    title: 'Change password · Church Attendance',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/auth/change-password.component').then((m) => m.ChangePasswordComponent)
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        title: 'Dashboard · Church Attendance',
        canActivate: [roleGuard],
        data: { roles: ['Admin', 'CellLeader'] },
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent)
      },
      {
        path: 'members',
        title: 'Members · Church Attendance',
        loadComponent: () =>
          import('./features/members/members.component').then((m) => m.MembersComponent)
      },
      {
        path: 'cells',
        title: 'Cells · Church Attendance',
        loadComponent: () => import('./features/cells/cells.component').then((m) => m.CellsComponent)
      },
      {
        path: 'cells/:id',
        title: 'Cell · Church Attendance',
        loadComponent: () =>
          import('./features/cells/cell-detail.component').then((m) => m.CellDetailComponent)
      },
      {
        path: 'events',
        title: 'Events · Church Attendance',
        loadComponent: () =>
          import('./features/events/events.component').then((m) => m.EventsComponent)
      },
      {
        path: 'events/:id/attendance',
        title: 'Attendance · Church Attendance',
        canActivate: [roleGuard],
        data: { roles: ['Admin', 'CellLeader'] },
        loadComponent: () =>
          import('./features/events/attendance.component').then((m) => m.AttendanceComponent)
      },
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard'
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
