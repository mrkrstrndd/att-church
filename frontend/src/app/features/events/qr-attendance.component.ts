import {
  AfterViewInit,
  Component,
  ElementRef,
  inject,
  NgZone,
  OnDestroy,
  OnInit,
  signal,
  ViewChild
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import QrScanner from 'qr-scanner';
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
  @ViewChild('scannerVideo') private videoRef?: ElementRef<HTMLVideoElement>;

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
  readonly flashAvailable = signal(false);
  readonly flashOn = signal(false);

  private readonly eventId = this.route.snapshot.queryParamMap.get('eventId') ?? '';
  private readonly dayId = this.route.snapshot.queryParamMap.get('dayId') ?? '';
  private scanner: QrScanner | null = null;
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

  async toggleFlash(): Promise<void> {
    const scanner = this.scanner;
    if (!scanner) {
      return;
    }
    try {
      await scanner.toggleFlash();
      this.flashOn.set(scanner.isFlashOn());
    } catch {
      this.flashAvailable.set(false);
    }
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
      const result = await QrScanner.scanImage(file, {
        returnDetailedScanResult: true,
        alsoTryWithoutScanRegion: true
      });
      this.handleDecoded(result.data);
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

    // The video lives inside the loaded-state template, so it can appear one
    // change-detection pass after the event data arrives.
    if (!this.videoRef?.nativeElement) {
      if (!this.destroyed) {
        setTimeout(() => this.maybeStartCamera(), 50);
      }
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

    const video = this.videoRef?.nativeElement;
    if (!video) {
      this.cameraState.set('error');
      this.cameraError.set('The camera view is not ready yet.');
      return;
    }

    this.cameraState.set('starting');
    this.cameraError.set('');

    try {
      this.scanner = new QrScanner(
        video,
        (result) => this.zone.run(() => this.handleDecoded(result.data)),
        {
          onDecodeError: () => {
            // No QR code in this frame.
          },
          preferredCamera: 'environment',
          maxScansPerSecond: 10,
          highlightScanRegion: true,
          highlightCodeOutline: true,
          returnDetailedScanResult: true
        }
      );
      await this.scanner.start();
      try {
        this.scanner.setInversionMode('both');
        this.flashAvailable.set(await this.scanner.hasFlash());
        this.flashOn.set(this.scanner.isFlashOn());
      } catch {
        this.flashAvailable.set(false);
      }
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
    this.flashAvailable.set(false);
    this.flashOn.set(false);
    if (!scanner) {
      return;
    }

    try {
      scanner.stop();
      scanner.destroy();
    } catch {
      // Scanner was not running.
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
