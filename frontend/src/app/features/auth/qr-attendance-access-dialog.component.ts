import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { EventService } from '../../core/services/event.service';
import { EventDay, EventListItem } from '../../core/models/event.model';
import { UserProfile } from '../../core/models/auth.model';
import { apiErrorMessage, formatDate } from '../../core/utils';

@Component({
  selector: 'app-qr-attendance-access-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule
  ],
  templateUrl: './qr-attendance-access-dialog.component.html',
  styleUrl: './qr-attendance-access-dialog.component.scss'
})
export class QrAttendanceAccessDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly eventsApi = inject(EventService);
  private readonly router = inject(Router);
  private readonly ref = inject(MatDialogRef<QrAttendanceAccessDialogComponent>);

  readonly step = signal<'credentials' | 'event'>('credentials');
  readonly loading = signal(false);
  readonly eventsLoading = signal(false);
  readonly daysLoading = signal(false);
  readonly error = signal('');
  readonly showPassword = signal(false);
  readonly events = signal<EventListItem[]>([]);
  readonly days = signal<EventDay[]>([]);
  readonly signedInUser = signal<UserProfile | null>(null);
  readonly selectedEvent = signal<EventListItem | null>(null);

  readonly form = this.fb.nonNullable.group({
    username: ['', [Validators.required, Validators.maxLength(50)]],
    password: ['', [Validators.required]],
    eventId: [''],
    dayId: ['']
  });

  private sessionCreated = false;

  verify(): void {
    this.error.set('');
    this.form.controls.username.markAsTouched();
    this.form.controls.password.markAsTouched();
    if (this.form.controls.username.invalid || this.form.controls.password.invalid) {
      return;
    }

    this.loading.set(true);
    this.auth
      .login({
        username: this.form.controls.username.value.trim(),
        password: this.form.controls.password.value,
        rememberMe: false
      })
      .subscribe({
        next: (response) => {
          this.loading.set(false);
          this.sessionCreated = true;

          if (!response.user.roles.includes('Admin')) {
            this.endSession();
            this.error.set('QR attendance is restricted to administrator accounts.');
            return;
          }
          if (response.user.mustChangePassword) {
            this.endSession();
            this.error.set('Change your password before opening the QR attendance station.');
            return;
          }

          this.signedInUser.set(response.user);
          this.step.set('event');
          this.loadEvents();
        },
        error: (error: unknown) => {
          this.loading.set(false);
          this.error.set(apiErrorMessage(error, 'Could not sign in. Check your credentials.'));
        }
      });
  }

  onEventChange(eventId: string): void {
    this.form.controls.dayId.setValue('');
    this.days.set([]);
    this.selectedEvent.set(this.events().find((event) => event.id === eventId) ?? null);
    if (!eventId) {
      return;
    }

    this.daysLoading.set(true);
    this.eventsApi.get(eventId).subscribe({
      next: (event) => {
        this.daysLoading.set(false);
        this.days.set(event.days);
        if (event.days.length === 1) {
          this.form.controls.dayId.setValue(event.days[0].id);
        }
      },
      error: (error: unknown) => {
        this.daysLoading.set(false);
        this.error.set(apiErrorMessage(error, 'Could not load this event.'));
      }
    });
  }

  start(): void {
    if (!this.selectedEvent() || !this.form.controls.dayId.value) {
      this.error.set('Choose an event and an event day first.');
      return;
    }

    this.ref.close(true);
    void this.router.navigate(['/qr-attendance'], {
      queryParams: {
        eventId: this.form.controls.eventId.value,
        dayId: this.form.controls.dayId.value
      }
    });
  }

  back(): void {
    this.endSession();
    this.step.set('credentials');
    this.signedInUser.set(null);
    this.events.set([]);
    this.days.set([]);
    this.selectedEvent.set(null);
    this.form.controls.eventId.setValue('');
    this.form.controls.dayId.setValue('');
    this.form.controls.password.setValue('');
    this.error.set('');
  }

  cancel(): void {
    this.endSession();
    this.ref.close();
  }

  togglePassword(): void {
    this.showPassword.update((value) => !value);
  }

  dayLabel(day: EventDay): string {
    return formatDate(day.eventDate);
  }

  eventDateLabel(event: EventListItem): string {
    return formatDate(event.startDate);
  }

  eventRangeLabel(event: EventListItem): string {
    return event.isMultiDay
      ? `${formatDate(event.startDate)} – ${formatDate(event.endDate)}`
      : formatDate(event.startDate);
  }

  private loadEvents(): void {
    this.eventsLoading.set(true);
    this.error.set('');
    this.eventsApi.list(1, 100).subscribe({
      next: (result) => {
        this.eventsLoading.set(false);
        this.events.set(result.items);
        if (result.items.length === 0) {
          this.error.set('No events are available yet. Create an event first.');
        }
      },
      error: (error: unknown) => {
        this.eventsLoading.set(false);
        this.error.set(apiErrorMessage(error, 'Could not load events.'));
      }
    });
  }

  private endSession(): void {
    if (!this.sessionCreated) {
      return;
    }
    this.sessionCreated = false;
    this.auth.logout().subscribe();
  }
}
