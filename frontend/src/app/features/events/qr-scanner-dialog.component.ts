import {
  AfterViewInit,
  Component,
  inject,
  NgZone,
  OnDestroy,
  signal
} from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Html5Qrcode } from 'html5-qrcode';
import { AttendanceService } from '../../core/services/attendance.service';
import { apiErrorMessage } from '../../core/utils';

export interface QrScannerDialogData {
  eventId: string;
  dayId: string;
  eventName: string;
  dayLabel: string;
}

interface ScanFeedback {
  state: 'success' | 'already' | 'error';
  title: string;
  detail: string;
}

@Component({
  selector: 'app-qr-scanner-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './qr-scanner-dialog.component.html',
  styleUrl: './qr-scanner-dialog.component.scss'
})
export class QrScannerDialogComponent implements AfterViewInit, OnDestroy {
  private readonly attendanceApi = inject(AttendanceService);
  private readonly zone = inject(NgZone);
  private readonly ref = inject(MatDialogRef<QrScannerDialogComponent, number>);
  readonly data = inject<QrScannerDialogData>(MAT_DIALOG_DATA);

  readonly cameraState = signal<'starting' | 'ready' | 'error' | 'paused'>('starting');
  readonly cameraError = signal('');
  readonly feedback = signal<ScanFeedback | null>(null);
  readonly sessionCount = signal(0);
  readonly busy = signal(false);
  readonly fileScanning = signal(false);

  private scanner: Html5Qrcode | null = null;
  private destroyed = false;
  private lastCode = '';
  private lastCodeAt = 0;

  ngAfterViewInit(): void {
    setTimeout(() => void this.startCamera(), 50);
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    void this.stopCamera();
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
    await this.stopCamera();
    this.cameraState.set('paused');

    try {
      const scanner = new Html5Qrcode('member-qr-scanner', { verbose: false });
      const decoded = await scanner.scanFile(file, true);
      scanner.clear();
      this.handleDecoded(decoded);
      await this.startCamera();
    } catch {
      this.feedback.set({
        state: 'error',
        title: 'QR code not detected',
        detail: 'Choose a clear image of the member QR code and try again.'
      });
      await this.startCamera();
    } finally {
      this.fileScanning.set(false);
    }
  }

  async retryCamera(): Promise<void> {
    await this.startCamera();
  }

  async close(): Promise<void> {
    await this.stopCamera();
    this.ref.close(this.sessionCount());
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
      this.scanner = new Html5Qrcode('member-qr-scanner', { verbose: false });
      await this.scanner.start(
        { facingMode: 'environment' },
        { fps: 10, qrbox: { width: 240, height: 240 }, aspectRatio: 1 },
        (decodedText) => this.zone.run(() => this.handleDecoded(decodedText)),
        () => {
          // No QR code in the current frame; keep scanning.
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
      // Scanner was not running, or the camera disappeared.
    }

    try {
      scanner.clear();
    } catch {
      // The scanner element was already removed.
    }
  }

  private handleDecoded(decodedText: string): void {
    const now = Date.now();
    if (
      this.busy() ||
      (this.lastCode === decodedText && now - this.lastCodeAt < 2500)
    ) {
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

    this.attendanceApi.scanQrCode(this.data.eventId, this.data.dayId, decodedText).subscribe({
      next: (result) => {
        this.sessionCount.update((count) => count + (result.alreadyCheckedIn ? 0 : 1));
        this.feedback.set({
          state: result.alreadyCheckedIn ? 'already' : 'success',
          title: result.alreadyCheckedIn ? 'Already checked in' : 'Checked in',
          detail: `${result.memberName} · ${result.checkedInCount} total for this day`
        });
        navigator.vibrate?.(result.alreadyCheckedIn ? 40 : [60, 40, 60]);
        setTimeout(() => this.busy.set(false), 1000);
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

  private cameraFailureMessage(error: unknown): string {
    const message = error instanceof Error ? error.message : String(error);
    if (/permission|notallowed/i.test(message)) {
      return 'Camera permission was denied. Allow camera access in your browser or upload a QR image below.';
    }
    if (/notfound|no camera|devices/i.test(message)) {
      return 'No camera was found on this device. Upload a QR image below instead.';
    }
    if (!window.isSecureContext) {
      return 'Camera scanning requires HTTPS (or localhost). Upload a QR image below instead.';
    }
    return 'The camera could not be started. Upload a QR image below instead.';
  }
}
