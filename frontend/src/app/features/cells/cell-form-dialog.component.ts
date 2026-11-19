import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { CellService } from '../../core/services/cell.service';
import { MemberService } from '../../core/services/member.service';
import { CellListItem } from '../../core/models/cell.model';
import { Member } from '../../core/models/member.model';
import { apiErrorMessage } from '../../core/utils';

export interface CellDialogData {
  cell?: CellListItem;
  cells: CellListItem[];
}

@Component({
  selector: 'app-cell-form-dialog',
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
  templateUrl: './cell-form-dialog.component.html',
  styleUrl: './cell-form-dialog.component.scss'
})
export class CellFormDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly cellsApi = inject(CellService);
  private readonly membersApi = inject(MemberService);
  private readonly ref = inject(MatDialogRef<CellFormDialogComponent, unknown>);
  readonly data = inject<CellDialogData>(MAT_DIALOG_DATA);

  readonly saving = signal(false);
  readonly error = signal('');
  readonly candidates = signal<Member[]>([]);
  readonly loadingCandidates = signal(true);
  readonly isEdit = !!this.data.cell;

  readonly form = this.fb.nonNullable.group({
    name: [this.data.cell?.name ?? '', [Validators.required, Validators.maxLength(100)]],
    description: [this.data.cell?.description ?? ''],
    leaderMemberId: [this.data.cell?.leaderMemberId ?? '', [Validators.required]]
  });

  readonly leaderOptions = computed(() => {
    const leadingIds = new Set(
      this.data.cells
        .filter((cell) => cell.id !== this.data.cell?.id)
        .map((cell) => cell.leaderMemberId)
    );
    return this.candidates().filter((member) => !leadingIds.has(member.id));
  });

  constructor() {
    this.membersApi
      .list({ role: 'CellLeader', page: 1, pageSize: 100 })
      .subscribe({
        next: (result) => {
          const members = result.items.filter((member) => member.isActive);
          if (
            this.data.cell &&
            !members.some((member) => member.id === this.data.cell?.leaderMemberId)
          ) {
            members.push({
              id: this.data.cell.leaderMemberId,
              lastName: '',
              firstName: '',
              fullName: this.data.cell.leaderName,
              email: '',
              phoneNo: null,
              cellId: null,
              cellName: null,
              roles: ['CellLeader'],
              hasLogin: false,
              isActive: true,
              createdAt: '',
              invitedByMemberId: null,
              invitedByName: null
            });
          }
          this.candidates.set(members);
          this.loadingCandidates.set(false);
        },
        error: (error: unknown) => {
          this.loadingCandidates.set(false);
          this.error.set(apiErrorMessage(error, 'Could not load cell leader candidates.'));
        }
      });
  }

  submit(): void {
    this.error.set('');
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) {
      return;
    }

    const value = this.form.getRawValue();
    const payload = {
      name: value.name.trim(),
      description: value.description.trim() || null,
      leaderMemberId: value.leaderMemberId
    };

    this.saving.set(true);
    const request = this.data.cell
      ? this.cellsApi.update(this.data.cell.id, payload)
      : this.cellsApi.create(payload);

    request.subscribe({
      next: (cell) => {
        this.saving.set(false);
        this.ref.close(cell);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(error, 'Could not save this cell.'));
      }
    });
  }

  cancel(): void {
    this.ref.close();
  }
}
