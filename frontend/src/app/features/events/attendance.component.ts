import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { EventFormDialogComponent } from '../events/event-form-dialog.component';
import { QrScannerDialogComponent, QrScannerDialogData } from './qr-scanner-dialog.component';
import { VipCheckInDialogComponent, VipCheckInDialogData } from './vip-check-in-dialog.component';
import { EventService } from '../../core/services/event.service';
import { AttendanceService } from '../../core/services/attendance.service';
import { AuthService } from '../../core/services/auth.service';
import { AttendanceDay, AttendanceMember, VipCheckInResult } from '../../core/models/attendance.model';
import { ChurchEvent } from '../../core/models/event.model';
import { apiErrorMessage, formatDate, formatDateTime } from '../../core/utils';

@Component({
  selector: 'app-attendance',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSnackBarModule,
    MatTooltipModule,
    PageHeaderComponent,
    EmptyStateComponent
  ],
  templateUrl: './attendance.component.html',
  styleUrl: './attendance.component.scss'
})
export class AttendanceComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly eventsApi = inject(EventService);
  private readonly attendanceApi = inject(AttendanceService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  readonly auth = inject(AuthService);

  readonly event = signal<ChurchEvent | null>(null);
  readonly day = signal<AttendanceDay | null>(null);
  readonly selectedDayId = signal('');
  readonly selectedMembers = signal<string[]>([]);
  readonly loading = signal(true);
  readonly loadingDay = signal(false);
  readonly checkingIn = signal(false);
  readonly search = this.fb.nonNullable.control('');

  private readonly eventId = this.route.snapshot.paramMap.get('id') ?? '';

  constructor() {
    this.search.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.selectedDayId()) {
          this.loadDay(this.search.value);
        }
      });
  }

  ngOnInit(): void {
    this.loadEvent();
  }

  loadEvent(preferredDayId?: string): void {
    this.loading.set(true);
    this.eventsApi.get(this.eventId).subscribe({
      next: (event) => {
        this.event.set(event);
        this.loading.set(false);
        if (event.days.length === 0) {
          return;
        }
        const dayId = preferredDayId && event.days.some((day) => day.id === preferredDayId)
          ? preferredDayId
          : this.selectedDayId() && event.days.some((day) => day.id === this.selectedDayId())
            ? this.selectedDayId()
            : event.days[0].id;
        this.selectDay(dayId);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.snackBar.open(apiErrorMessage(error, 'Could not load this event.'), 'Close', {
          duration: 5000
        });
      }
    });
  }

  selectDay(dayId: string): void {
    this.selectedDayId.set(dayId);
    this.selectedMembers.set([]);
    this.search.setValue('', { emitEvent: false });
    this.loadDay('');
  }

  loadDay(search: string): void {
    const dayId = this.selectedDayId();
    if (!dayId) {
      return;
    }
    this.loadingDay.set(true);
    this.attendanceApi.getDay(this.eventId, dayId, search).subscribe({
      next: (day) => {
        this.day.set(day);
        this.event.update((current) =>
          current
            ? {
                ...current,
                days: current.days.map((eventDay) =>
                  eventDay.id === day.dayId
                    ? { ...eventDay, attendanceCount: day.checkedInCount }
                    : eventDay
                )
              }
            : current
        );
        this.loadingDay.set(false);
      },
      error: (error: unknown) => {
        this.loadingDay.set(false);
        this.snackBar.open(apiErrorMessage(error, 'Could not load attendance.'), 'Close', {
          duration: 5000
        });
      }
    });
  }

  openQrScanner(): void {
    const event = this.event();
    const day = this.day();
    if (!event || !day) {
      return;
    }

    const data: QrScannerDialogData = {
      eventId: event.id,
      dayId: day.dayId,
      eventName: event.name,
      dayLabel: this.dayLabel(day.eventDate)
    };

    this.dialog
      .open(QrScannerDialogComponent, {
        data,
        width: '500px',
        maxWidth: '94vw',
        autoFocus: false
      })
      .afterClosed()
      .subscribe((scanned) => {
        if (scanned) {
          this.snackBar.open(
            `${scanned} ${scanned === 1 ? 'person' : 'people'} checked in by QR.`,
            'Close',
            { duration: 3500 }
          );
        }
        this.loadDay(this.search.value);
      });
  }

  openVipCheckIn(): void {
    const event = this.event();
    const day = this.day();
    if (!event || !day) {
      return;
    }

    const data: VipCheckInDialogData = {
      eventId: event.id,
      dayId: day.dayId,
      eventName: event.name,
      dayLabel: this.dayLabel(day.eventDate)
    };

    this.dialog
      .open(VipCheckInDialogComponent, {
        data,
        width: '640px',
        maxWidth: '94vw',
        autoFocus: false
      })
      .afterClosed()
      .subscribe((result: VipCheckInResult | undefined) => {
        if (result) {
          const message = result.alreadyCheckedIn
            ? `${result.memberName} is already checked in.`
            : result.created
              ? `${result.memberName} was added as a VIP and checked in.`
              : `${result.memberName} checked in as a VIP.`;
          this.snackBar.open(message, 'Close', { duration: 4000 });
        }
        this.loadDay(this.search.value);
      });
  }

  toggleSelection(memberId: string, checked: boolean): void {
    this.selectedMembers.update((current) =>
      checked ? [...new Set([...current, memberId])] : current.filter((id) => id !== memberId)
    );
  }

  isSelected(memberId: string): boolean {
    return this.selectedMembers().includes(memberId);
  }

  checkInSelected(): void {
    const day = this.day();
    if (!day || this.selectedMembers().length === 0 || this.checkingIn()) {
      return;
    }

    this.checkingIn.set(true);
    this.attendanceApi.checkIn(this.eventId, day.dayId, this.selectedMembers()).subscribe({
      next: (result) => {
        this.checkingIn.set(false);
        this.selectedMembers.set([]);
        this.snackBar.open(
          result.added > 0
            ? `${result.added} ${result.added === 1 ? 'person' : 'people'} checked in.`
            : 'Those members were already checked in.',
          'Close',
          { duration: 3000 }
        );
        this.loadDay(this.search.value);
      },
      error: (error: unknown) => {
        this.checkingIn.set(false);
        this.snackBar.open(apiErrorMessage(error, 'Could not check in these members.'), 'Close', {
          duration: 5000
        });
      }
    });
  }

  undoCheckIn(member: AttendanceMember): void {
    const day = this.day();
    if (!day) {
      return;
    }
    this.attendanceApi.remove(this.eventId, day.dayId, member.id).subscribe({
      next: () => {
        this.snackBar.open(`${member.fullName} was removed from this attendance sheet.`, 'Close', {
          duration: 3000
        });
        this.loadDay(this.search.value);
      },
      error: (error: unknown) =>
        this.snackBar.open(apiErrorMessage(error, 'Could not undo this check-in.'), 'Close', {
          duration: 5000
        })
    });
  }

  editEvent(): void {
    const current = this.event();
    if (!current) {
      return;
    }
    this.dialog
      .open(EventFormDialogComponent, {
        data: { event: current },
        width: '640px',
        maxWidth: '94vw'
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.snackBar.open('Event updated.', 'Close', { duration: 3000 });
          this.loadEvent(this.selectedDayId());
        }
      });
  }

  dayLabel(date: string): string {
    return new Intl.DateTimeFormat('en-US', {
      weekday: 'short',
      month: 'short',
      day: 'numeric'
    }).format(new Date(`${date}T00:00:00`));
  }

  dateRange(): string {
    const event = this.event();
    if (!event) {
      return '';
    }
    return event.isMultiDay
      ? `${formatDate(event.startDate)} – ${formatDate(event.endDate)}`
      : formatDate(event.startDate);
  }

  checkedInTime(member: AttendanceMember): string {
    return member.checkedInAt ? formatDateTime(member.checkedInAt) : '';
  }

  memberInitials(member: AttendanceMember): string {
    return member.fullName
      .split(' ')
      .slice(0, 2)
      .map((part) => part.charAt(0))
      .join('')
      .toUpperCase();
  }

  roleClass(role: string): string {
    return `role-chip role-chip--${role.toLowerCase()}`;
  }
}
