import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialog, MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import * as QRCode from 'qrcode';
import { MemberService } from '../../core/services/member.service';
import { Member, MemberPayload, MemberQrCode } from '../../core/models/member.model';
import { CellListItem } from '../../core/models/cell.model';
import { Role } from '../../core/models/auth.model';
import { AuthService } from '../../core/services/auth.service';
import { apiErrorMessage } from '../../core/utils';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../shared/confirm-dialog.component';

export interface MemberDialogData {
  member?: Member;
  cells: CellListItem[];
}

@Component({
  selector: 'app-member-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSlideToggleModule
  ],
  templateUrl: './member-form-dialog.component.html',
  styleUrl: './member-form-dialog.component.scss'
})
export class MemberFormDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly members = inject(MemberService);
  private readonly dialog = inject(MatDialog);
  readonly auth = inject(AuthService);
  private readonly ref = inject(MatDialogRef<MemberFormDialogComponent, Member>);
  readonly data = inject<MemberDialogData>(MAT_DIALOG_DATA);

  readonly saving = signal(false);
  readonly error = signal('');
  readonly showPassword = signal(false);
  readonly qrCode = signal<MemberQrCode | null>(null);
  readonly qrImage = signal<string | null>(null);
  readonly qrLoading = signal(false);
  readonly qrError = signal('');
  readonly isEdit = !!this.data.member;

  constructor() {
    if (this.data.member) {
      this.loadQrCode(this.data.member.id);
    }
  }

  readonly form = this.fb.nonNullable.group({
    lastName: [this.data.member?.lastName ?? '', [Validators.required, Validators.maxLength(100)]],
    firstName: [this.data.member?.firstName ?? '', [Validators.required, Validators.maxLength(100)]],
    email: [this.data.member?.email ?? '', [Validators.required, Validators.email, Validators.maxLength(255)]],
    phoneNo: [
      this.data.member?.phoneNo ?? '',
      [Validators.pattern(/^\+?[0-9\s\-]{7,20}$/)]
    ],
    cellId: [this.data.member?.cellId ?? ''],
    roles: this.fb.nonNullable.group({
      Member: [this.data.member ? this.data.member.roles.includes('Member') : true],
      VIP: [this.data.member?.roles.includes('VIP') ?? false],
      CellLeader: [this.data.member?.roles.includes('CellLeader') ?? false],
      Admin: [this.data.member?.roles.includes('Admin') ?? false]
    }),
    createLogin: [false],
    username: [''],
    password: ['']
  });

  get roles(): Role[] {
    const values = this.form.controls.roles.getRawValue();
    const result: Role[] = [];
    if (values.Member) {
      result.push('Member');
    }
    if (values.VIP) {
      result.push('VIP');
    }
    if (values.CellLeader) {
      result.push('CellLeader');
    }
    if (values.Admin) {
      result.push('Admin');
    }
    return result;
  }

  get wantsLogin(): boolean {
    return this.auth.isAdmin() && !this.isEdit && this.form.controls.createLogin.value;
  }

  onRoleChanged(role: Role, checked: boolean): void {
    const roles = this.form.controls.roles;
    if (role === 'VIP' && checked) {
      roles.patchValue({ Member: false, VIP: true, CellLeader: false, Admin: false });
      this.form.controls.cellId.setValue('');
      return;
    }

    if (role !== 'VIP' && checked && roles.controls.VIP.value) {
      roles.controls.VIP.setValue(false);
    }
  }

  onCellChanged(cellId: string): void {
    if (!cellId || !this.form.controls.roles.controls.VIP.value) {
      return;
    }

    this.form.controls.roles.patchValue({ VIP: false, Member: true });
  }

  togglePassword(): void {
    this.showPassword.update((value) => !value);
  }

  reloadQrCode(): void {
    if (this.data.member) {
      this.loadQrCode(this.data.member.id);
    }
  }

  submit(): void {
    this.error.set('');
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      return;
    }
    if (this.roles.length === 0) {
      this.error.set('Choose at least one role.');
      return;
    }

    if (this.wantsLogin) {
      const username = this.form.controls.username.value.trim();
      const password = this.form.controls.password.value;
      if (username.length < 3 || !/^[a-zA-Z0-9._-]+$/.test(username)) {
        this.error.set('Username must be at least 3 characters and use letters, numbers, dots, underscores, or hyphens.');
        return;
      }
      if (!isStrongPassword(password)) {
        this.error.set('The temporary password does not meet the password requirements.');
        return;
      }
    }

    const value = this.form.getRawValue();
    const payload: MemberPayload = {
      lastName: value.lastName.trim(),
      firstName: value.firstName.trim(),
      email: value.email.trim(),
      phoneNo: value.phoneNo.trim() || null,
      cellId: value.cellId || null,
      roles: this.roles
    };

    if (!this.isEdit && value.createLogin) {
      payload.createLogin = {
        username: value.username.trim(),
        password: value.password
      };
    }

    this.saving.set(true);
    const request = this.data.member
      ? this.members.update(this.data.member.id, payload)
      : this.members.create(payload);

    request.subscribe({
      next: (member) => {
        this.saving.set(false);
        this.ref.close(member);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(error, 'Could not save this member.'));
      }
    });
  }

  cancel(): void {
    this.ref.close();
  }

  downloadQrCode(): void {
    const member = this.data.member;
    const image = this.qrImage();
    if (!member || !image) {
      return;
    }

    const safeName = member.fullName
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-|-$/g, '');
    const link = document.createElement('a');
    link.href = image;
    link.download = `${safeName || 'member'}-attendance-qr.png`;
    link.click();
  }

  printQrCode(): void {
    const member = this.data.member;
    const image = this.qrImage();
    if (!member || !image) {
      return;
    }

    const printWindow = window.open('', '_blank', 'width=620,height=760');
    if (!printWindow) {
      this.qrError.set('Allow pop-ups for this site to print the QR code.');
      return;
    }

    printWindow.document.write(`
      <!doctype html>
      <html>
        <head>
          <title>${escapeHtml(member.fullName)} — Attendance QR</title>
          <style>
            body { margin: 0; padding: 48px; font-family: Arial, sans-serif; color: #142b2f; text-align: center; }
            .card { max-width: 420px; margin: 0 auto; padding: 32px; border: 1px solid #d9d2c2; border-radius: 18px; }
            img { width: 320px; height: 320px; image-rendering: pixelated; }
            h1 { margin: 0 0 6px; font-size: 24px; }
            p { margin: 8px 0 20px; color: #74807c; font-size: 13px; }
            small { display: block; margin-top: 18px; color: #9b763a; font-size: 10px; letter-spacing: 1.4px; text-transform: uppercase; }
          </style>
        </head>
        <body>
          <div class="card">
            <h1>${escapeHtml(member.fullName)}</h1>
            <p>Church attendance QR pass</p>
            <img src="${image}" alt="Member QR code" />
            <small>Present this code at check-in</small>
          </div>
          <script>window.onload = () => window.print();</script>
        </body>
      </html>
    `);
    printWindow.document.close();
  }

  rotateQrCode(): void {
    const member = this.data.member;
    if (!member) {
      return;
    }

    const data: ConfirmDialogData = {
      title: 'Regenerate QR code?',
      message:
        'The current QR code will stop working immediately. Only do this if the member’s code was lost or shared incorrectly.',
      confirmLabel: 'Regenerate',
      destructive: true
    };

    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px', maxWidth: '92vw' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }

        this.qrLoading.set(true);
        this.qrError.set('');
        this.qrImage.set(null);
        this.members.rotateQrCode(member.id).subscribe({
          next: async (qr) => {
            this.qrCode.set(qr);
            try {
              await this.renderQrCode(qr.payload);
            } catch {
              this.qrError.set('The new QR code could not be rendered.');
            } finally {
              this.qrLoading.set(false);
            }
          },
          error: (error: unknown) => {
            this.qrLoading.set(false);
            this.qrError.set(apiErrorMessage(error, 'Could not regenerate the QR code.'));
          }
        });
      });
  }

  private loadQrCode(memberId: string): void {
    this.qrLoading.set(true);
    this.qrError.set('');
    this.qrImage.set(null);
    this.members.getQrCode(memberId).subscribe({
      next: async (qr) => {
        this.qrCode.set(qr);
        try {
          await this.renderQrCode(qr.payload);
        } catch {
          this.qrError.set('The QR code could not be rendered.');
        } finally {
          this.qrLoading.set(false);
        }
      },
      error: (error: unknown) => {
        this.qrLoading.set(false);
        this.qrError.set(apiErrorMessage(error, 'Could not load this member’s QR code.'));
      }
    });
  }

  private async renderQrCode(payload: string): Promise<void> {
    const image = await QRCode.toDataURL(payload, {
      errorCorrectionLevel: 'M',
      width: 640,
      margin: 2,
      color: {
        dark: '#142b2f',
        light: '#fffdf8'
      }
    });
    this.qrImage.set(image);
  }
}

function isStrongPassword(password: string): boolean {
  return (
    password.length >= 8 &&
    /[A-Z]/.test(password) &&
    /[a-z]/.test(password) &&
    /[0-9]/.test(password) &&
    /[^A-Za-z0-9]/.test(password)
  );
}

function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, (character) => {
    switch (character) {
      case '&':
        return '&amp;';
      case '<':
        return '&lt;';
      case '>':
        return '&gt;';
      case '"':
        return '&quot;';
      default:
        return '&#39;';
    }
  });
}
