import {
  AfterViewInit,
  Component,
  inject,
  NgZone,
  OnDestroy,
  OnInit,
  signal
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Html5Qrcode } from 'html5-qrcode';
import { AttendanceService } from '../../core/services/attendance.service';
import { AuthService } from '../../core/services/auth.service';
import { EventService } from '../../core/services/event.service';
import { ChurchEvent, EventDay } from '../../core/models/event.model';
import { CopyrightComponent } from '../../shared/copyright.component';
import { apiErrorMessage, formatDate } from '../../core/utils';

interface ScannedAttendee {
  memberId: string;
  memberName: string;
  scannedAt: Date;
}

interface ScanFeedback {
  state: 'success' | 'already' | 'error';
  title: string;
  detail: string;
}

@Component({
  selector: 'app-qr-attendance',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule, CopyrightComponent],
  templateUrl: './qr-attendance.component.html',
  styleUrl: './qr-attendance.component.scss'
})
export class QrAttendanceComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly eventsApi = inject(EventService);
  private readonly attendanceApi = inject(AttendanceService);
  private readonly auth = inject(AuthService);
  private readonly zone = inject(NgZone);

  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly event = signal<ChurchEvent | null>(null);
  readonly eventDay = signal<EventDay | null>(null);
  readonly cameraState = signal<'starting' | 'ready' | 'error' | 'paused'>('starting');
  readonly cameraError = signal('');
  readonly feedback = signal<ScanFeedback | null>(null);
  readonly scans = signal<ScannedAttendee[]>([]);
  readonly totalCheckedIn = signal(0);
  readonly busy = signal(false);
  readonly fileScanning = signal(false);

  private readonly eventId = this.route.snapshot.queryParamMap.get('eventId') ?? '';
  private readonly dayId = this.route.snapshot.queryParamMap.get('dayId') ?? '';
  private scanner: Html5Qrcode | null = null;
  private destroyed = false;
  private viewReady = false;
  private cameraStarted = false;
  private lastCode = '';
  private lastCodeAt = 0;

  ngOnInit(): void {
    if (!this.eventId || !this.dayId) {
      void this.router.navigate(['/login']);
      return;
    }

    this.eventsApi.get(this.eventId).subscribe({
      next: (event) => {
        const day = event.days.find((item) => item.id === this.dayId);
        if (!day) {
          this.loading.set(false);
          this.loadError.set('That event day could not be found.');
          return;
        }

        this.event.set(event);
        this.eventDay.set(day);
        this.totalCheckedIn.set(day.attendanceCount);
        this.loading.set(false);
        this.loadDayCount();
        this.maybeStartCamera();
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.loadError.set(apiErrorMessage(error, 'Could not load this event.'));
      }
    });
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.maybeStartCamera();
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    void this.stopCamera();
  }

  async retryCamera(): Promise<void> {
    await this.startCamera();
  }

  async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }

    this.fileScanning.set(true);
    this.feedback.set(null);
    this.lastCode = '';
    await this.stopCamera();
    this.cameraState.set('paused');

    try {
      const scanner = new Html5Qrcode('qr-attendance-camera', { verbose: false });
      const decoded = await scanner.scanFile(file, true);
      scanner.clear();
      this.handleDecoded(decoded);
      await this.startCamera();
    } catch {
      this.feedback.set({
        state: 'error',
        title: 'QR code not detected',
        detail: 'Choose a clear image of the member QR pass and try again.'
      });
      await this.startCamera();
    } finally {
      this.fileScanning.set(false);
    }
  }

  async exit(): Promise<void> {
    await this.stopCamera();
    this.auth.logout().subscribe(() => {
      void this.router.navigate(['/login']);
    });
  }

  dayLabel(): string {
    const day = this.eventDay();
    return day ? formatDate(day.eventDate) : '';
  }

  rangeLabel(): string {
    const event = this.event();
    if (!event) {
      return '';
    }
    return event.isMultiDay
      ? `${formatDate(event.startDate)} – ${formatDate(event.endDate)}`
      : formatDate(event.startDate);
  }

  contextLabel(): string {
    const event = this.event();
    if (!event) {
      return '';
    }
    return event.isMultiDay ? `${this.dayLabel()} · ${this.rangeLabel()}` : this.dayLabel();
  }

  initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part.charAt(0))
      .join('')
      .toUpperCase();
  }

  timeLabel(value: Date): string {
    return new Intl.DateTimeFormat('en-US', {
      hour: 'numeric',
      minute: '2-digit'
    }).format(value);
  }

  private maybeStartCamera(): void {
    if (this.cameraStarted || !this.viewReady || !this.event() || !this.eventDay()) {
      return;
    }
    this.cameraStarted = true;
    void this.startCamera();
  }

  private async startCamera(): Promise<void> {
    if (this.destroyed) {
      return;
    }

    await this.stopCamera();
    if (this.destroyed) {
      return;
    }

    this.cameraState.set('starting');
    this.cameraError.set('');

    try {
      this.scanner = new Html5Qrcode('qr-attendance-camera', { verbose: false });
      await this.scanner.start(
        { facingMode: 'environment' },
        {
          fps: 10,
          qrbox: { width: 280, height: 280 },
          aspectRatio: 1.45
        },
        (decodedText) => this.zone.run(() => this.handleDecoded(decodedText)),
        () => {
          // No QR code in this frame.
        }
      );
      this.zone.run(() => this.cameraState.set('ready'));
    } catch (error: unknown) {
      this.zone.run(() => {
        this.cameraState.set('error');
        this.cameraError.set(this.cameraFailureMessage(error));
      });
    }
  }

  private async stopCamera(): Promise<void> {
    const scanner = this.scanner;
    this.scanner = null;
    if (!scanner) {
      return;
    }

    try {
      await scanner.stop();
    } catch {
      // Scanner was not running.
    }

    try {
      scanner.clear();
    } catch {
      // Scanner element was removed.
    }
  }

  private handleDecoded(decodedText: string): void {
    const now = Date.now();
    if (this.busy() || (this.lastCode === decodedText && now - this.lastCodeAt < 2500)) {
      return;
    }

    this.lastCode = decodedText;
    this.lastCodeAt = now;
    this.busy.set(true);
    this.feedback.set({
      state: 'success',
      title: 'Checking in…',
      detail: 'Looking up this member.'
    });

    this.attendanceApi.scanQrCode(this.eventId, this.dayId, decodedText).subscribe({
      next: (result) => {
        this.totalCheckedIn.set(result.checkedInCount);
        if (!result.alreadyCheckedIn) {
          this.scans.update((scans) => [
            {
              memberId: result.memberId,
              memberName: result.memberName,
              scannedAt: new Date()
            },
            ...scans
          ]);
        }

        this.feedback.set({
          state: result.alreadyCheckedIn ? 'already' : 'success',
          title: result.alreadyCheckedIn ? 'Already checked in' : 'Checked in',
          detail: result.alreadyCheckedIn
            ? `${result.memberName} was already marked present.`
            : `${result.memberName} is now marked present.`
        });
        navigator.vibrate?.(result.alreadyCheckedIn ? 40 : [60, 40, 60]);
        setTimeout(() => this.busy.set(false), 900);
      },
      error: (error: unknown) => {
        this.feedback.set({
          state: 'error',
          title: 'Not checked in',
          detail: apiErrorMessage(error, 'This QR code could not be used for check-in.')
        });
        setTimeout(() => this.busy.set(false), 1200);
      }
    });
  }

  private loadDayCount(): void {
    this.attendanceApi.getDay(this.eventId, this.dayId).subscribe({
      next: (day) => this.totalCheckedIn.set(day.checkedInCount),
      error: () => {
        // The scanner can still check in even if the initial count cannot load.
      }
    });
  }

  private cameraFailureMessage(error: unknown): string {
    const message = error instanceof Error ? error.message : String(error);
    if (/permission|notallowed/i.test(message)) {
      return 'Camera permission was denied. Allow camera access, or upload a QR image.';
    }
    if (/notfound|no camera|devices/i.test(message)) {
      return 'No camera was found. Upload a QR image instead.';
    }
    if (!window.isSecureContext) {
      return 'Camera scanning requires HTTPS or localhost. Upload a QR image instead.';
    }
    return 'The camera could not be started. Upload a QR image instead.';
  }
}
