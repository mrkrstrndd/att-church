import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { debounceTime, distinctUntilChanged, finalize, switchMap, tap } from 'rxjs';
import { AttendanceService } from '../../core/services/attendance.service';
import { MemberService } from '../../core/services/member.service';
import { InviterOption, VipLookup } from '../../core/models/member.model';
import { apiErrorMessage } from '../../core/utils';

export interface VipCheckInDialogData {
  eventId: string;
  dayId: string;
  eventName: string;
  dayLabel: string;
}

@Component({
  selector: 'app-vip-check-in-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './vip-check-in-dialog.component.html',
  styleUrl: './vip-check-in-dialog.component.scss'
})
export class VipCheckInDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly members = inject(MemberService);
  private readonly attendance = inject(AttendanceService);
  private readonly ref = inject(MatDialogRef<VipCheckInDialogComponent, unknown>);
  private readonly destroyRef = inject(DestroyRef);
  readonly data = inject<VipCheckInDialogData>(MAT_DIALOG_DATA);

  readonly saving = signal(false);
  readonly error = signal('');
  readonly lookupLoading = signal(false);
  readonly lookupMatches = signal<VipLookup[]>([]);
  readonly selectedExisting = signal<VipLookup | null>(null);
  readonly createNew = signal(false);
  readonly inviterOptions = signal<InviterOption[]>([]);
  readonly selectedInviter = signal<InviterOption | null>(null);
  readonly inviterLoading = signal(false);

  readonly form = this.fb.nonNullable.group({
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(255)]],
    phoneNo: ['', [Validators.pattern(/^\+?[0-9\s\-]{7,20}$/)]],
    invitedByMemberId: ['']
  });

  readonly inviterSearch = this.fb.nonNullable.control('');

  constructor() {
    this.form.controls.lastName.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => {
        if (this.selectedExisting()) {
          this.clearExistingSelection(true);
        }
        this.createNew.set(false);
        this.lookupVips(value);
      });

    this.inviterSearch.valueChanges
      .pipe(
        tap((value) => {
          const selected = this.selectedInviter();
          if (selected && value !== selected.fullName) {
            this.selectedInviter.set(null);
            this.form.controls.invitedByMemberId.setValue('');
          }
        }),
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((search) => {
          this.inviterLoading.set(true);
          return this.members.inviterOptions(search).pipe(
            finalize(() => this.inviterLoading.set(false))
          );
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (options) => this.inviterOptions.set(options),
        error: () => this.inviterOptions.set([])
      });
  }

  ngOnInit(): void {
    this.loadInviterOptions('');
  }

  get isExisting(): boolean {
    return this.selectedExisting() !== null;
  }

  submitDisabled(): boolean {
    if (this.saving()) {
      return true;
    }
    if (this.selectedExisting()) {
      return false;
    }
    if (this.lookupMatches().length > 0 && !this.createNew()) {
      return true;
    }
    return this.form.invalid;
  }

  selectExisting(vip: VipLookup): void {
    this.selectedExisting.set(vip);
    this.createNew.set(false);
    this.form.controls.lastName.setValue(vip.lastName, { emitEvent: false });
    this.form.controls.firstName.setValue(vip.firstName, { emitEvent: false });
    this.form.controls.email.setValue(vip.email, { emitEvent: false });
    this.form.controls.phoneNo.setValue(vip.phoneNo ?? '', { emitEvent: false });
    this.form.controls.firstName.disable({ emitEvent: false });
    this.form.controls.email.disable({ emitEvent: false });
    this.form.controls.phoneNo.disable({ emitEvent: false });
    this.form.controls.firstName.updateValueAndValidity();
    this.form.controls.email.updateValueAndValidity();
    this.form.controls.phoneNo.updateValueAndValidity();
  }

  startNewVip(): void {
    this.selectedExisting.set(null);
    this.createNew.set(true);
    this.form.controls.firstName.enable({ emitEvent: false });
    this.form.controls.email.enable({ emitEvent: false });
    this.form.controls.phoneNo.enable({ emitEvent: false });
    this.form.controls.firstName.setValue('');
    this.form.controls.email.setValue('');
    this.form.controls.phoneNo.setValue('');
  }

  useExistingMatches(): void {
    this.createNew.set(false);
    if (this.lookupMatches().length === 1) {
      this.selectExisting(this.lookupMatches()[0]);
    }
  }

  selectInviter(event: MatAutocompleteSelectedEvent): void {
    const inviter = event.option.value as InviterOption;
    this.selectedInviter.set(inviter);
    this.form.controls.invitedByMemberId.setValue(inviter.id);
    this.inviterSearch.setValue(inviter.fullName, { emitEvent: false });
  }

  clearInviter(): void {
    this.selectedInviter.set(null);
    this.form.controls.invitedByMemberId.setValue('');
    this.inviterSearch.setValue('', { emitEvent: false });
  }

  submit(): void {
    this.error.set('');
    this.form.markAllAsTouched();

    const existing = this.selectedExisting();
    if (!existing && this.form.invalid) {
      return;
    }
    if (!existing && this.lookupMatches().length > 0 && !this.createNew()) {
      this.error.set('Select the existing VIP account or choose “Create a new VIP instead”.');
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();
    const payload = existing
      ? {
          existingMemberId: existing.id,
          lastName: null,
          firstName: null,
          email: null,
          phoneNo: null,
          invitedByMemberId: null
        }
      : {
          existingMemberId: null,
          lastName: value.lastName.trim(),
          firstName: value.firstName.trim(),
          email: value.email.trim(),
          phoneNo: value.phoneNo.trim() || null,
          invitedByMemberId: value.invitedByMemberId || null
        };

    this.attendance.createVipCheckIn(this.data.eventId, this.data.dayId, payload).subscribe({
      next: (result) => {
        this.saving.set(false);
        this.ref.close(result);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(error, 'Could not check in this VIP.'));
      }
    });
  }

  cancel(): void {
    this.ref.close();
  }

  initials(vip: VipLookup): string {
    return `${vip.firstName.charAt(0)}${vip.lastName.charAt(0)}`.toUpperCase();
  }

  private lookupVips(lastName: string): void {
    const term = lastName.trim();
    if (term.length < 2) {
      this.lookupMatches.set([]);
      this.lookupLoading.set(false);
      return;
    }

    this.lookupLoading.set(true);
    this.members
      .lookupVips(term)
      .pipe(finalize(() => this.lookupLoading.set(false)))
      .subscribe({
        next: (matches) => {
          this.lookupMatches.set(matches);
          if (matches.length === 1 && !this.createNew()) {
            this.selectExisting(matches[0]);
          }
        },
        error: () => this.lookupMatches.set([])
      });
  }

  private clearExistingSelection(resetFields: boolean): void {
    this.selectedExisting.set(null);
    this.form.controls.firstName.enable({ emitEvent: false });
    this.form.controls.email.enable({ emitEvent: false });
    this.form.controls.phoneNo.enable({ emitEvent: false });
    if (resetFields) {
      this.form.controls.firstName.setValue('');
      this.form.controls.email.setValue('');
      this.form.controls.phoneNo.setValue('');
    }
  }

  private loadInviterOptions(search: string): void {
    this.inviterLoading.set(true);
    this.members
      .inviterOptions(search)
      .pipe(finalize(() => this.inviterLoading.set(false)))
      .subscribe({
        next: (options) => this.inviterOptions.set(options),
        error: () => this.inviterOptions.set([])
      });
  }
}
