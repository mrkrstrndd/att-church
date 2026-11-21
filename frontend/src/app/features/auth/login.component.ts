import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from '../../core/services/auth.service';
import { apiErrorMessage } from '../../core/utils';
import { CopyrightComponent } from '../../shared/copyright.component';
import { QrAttendanceAccessDialogComponent } from './qr-attendance-access-dialog.component';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    CopyrightComponent
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);

  readonly loading = signal(false);
  readonly error = signal('');
  readonly showPassword = signal(false);

  readonly form = this.fb.nonNullable.group({
    username: ['', [Validators.required, Validators.maxLength(50)]],
    password: ['', [Validators.required]],
    rememberMe: [false]
  });

  togglePassword(): void {
    this.showPassword.update((value) => !value);
  }

  openQrAttendance(): void {
    this.dialog.open(QrAttendanceAccessDialogComponent, {
      width: '500px',
      maxWidth: '94vw',
      autoFocus: false,
      disableClose: true
    });
  }

  submit(): void {
    this.error.set('');
    if (this.form.invalid || this.loading()) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.auth.login(this.form.getRawValue()).subscribe({
      next: (response) => {
        this.loading.set(false);
        if (response.user.mustChangePassword) {
          void this.router.navigate(['/change-password']);
          return;
        }

        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        const target = returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('/login')
          ? returnUrl
          : '/dashboard';
        void this.router.navigateByUrl(target);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(apiErrorMessage(error, 'Invalid username or password.'));
      }
    });
  }
}
