import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { EventService } from '../../core/services/event.service';
import { ChurchEvent } from '../../core/models/event.model';
import { apiErrorMessage, fromIsoDate, toIsoDate } from '../../core/utils';

export interface EventDialogData {
  event?: ChurchEvent;
}

@Component({
  selector: 'app-event-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule
  ],
  templateUrl: './event-form-dialog.component.html',
  styleUrl: './event-form-dialog.component.scss'
})
export class EventFormDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly events = inject(EventService);
  private readonly ref = inject(MatDialogRef<EventFormDialogComponent, ChurchEvent>);
  readonly data = inject<EventDialogData>(MAT_DIALOG_DATA);

  readonly saving = signal(false);
  readonly error = signal('');
  readonly isEdit = !!this.data.event;

  readonly form = this.fb.nonNullable.group(
    {
      name: [this.data.event?.name ?? '', [Validators.required, Validators.maxLength(150)]],
      description: [this.data.event?.description ?? ''],
      isMultiDay: [this.data.event?.isMultiDay ?? false],
      startDate: [fromIsoDate(this.data.event?.startDate) as Date | null, [Validators.required]],
      endDate: [fromIsoDate(this.data.event?.endDate) as Date | null]
    },
    { validators: dateRangeValid }
  );

  get isMultiDay(): boolean {
    return this.form.controls.isMultiDay.value;
  }

  toggleMultiDay(): void {
    if (!this.isMultiDay) {
      this.form.controls.endDate.setValue(this.form.controls.startDate.value);
    }
    this.form.updateValueAndValidity();
  }

  submit(): void {
    this.error.set('');
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) {
      return;
    }

    const value = this.form.getRawValue();
    const startDate = toIsoDate(value.startDate);
    const endDate = value.isMultiDay ? toIsoDate(value.endDate) : startDate;
    if (!startDate || !endDate) {
      this.error.set('Choose a valid date.');
      return;
    }

    const payload = {
      name: value.name.trim(),
      description: value.description.trim() || null,
      isMultiDay: value.isMultiDay,
      startDate,
      endDate
    };

    this.saving.set(true);
    const request = this.data.event
      ? this.events.update(this.data.event.id, payload)
      : this.events.create(payload);

    request.subscribe({
      next: (event) => {
        this.saving.set(false);
        this.ref.close(event);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(error, 'Could not save this event.'));
      }
    });
  }

  cancel(): void {
    this.ref.close();
  }
}

function dateRangeValid(control: AbstractControl): ValidationErrors | null {
  const isMultiDay = control.get('isMultiDay')?.value as boolean;
  if (!isMultiDay) {
    return null;
  }
  const start = control.get('startDate')?.value as Date | null;
  const end = control.get('endDate')?.value as Date | null;
  if (!start || !end) {
    return { dateRangeRequired: true };
  }
  return start <= end ? null : { dateRangeInvalid: true };
}
