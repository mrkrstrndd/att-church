import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule, MatSelectionListChange } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { CellService } from '../../core/services/cell.service';
import { MemberService } from '../../core/services/member.service';
import { Member } from '../../core/models/member.model';
import { apiErrorMessage } from '../../core/utils';

export interface AddMembersDialogData {
  cellId: string;
  cellName: string;
  existingMemberIds: string[];
}

@Component({
  selector: 'app-add-members-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatListModule,
    MatProgressBarModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './add-members-dialog.component.html',
  styleUrl: './add-members-dialog.component.scss'
})
export class AddMembersDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly membersApi = inject(MemberService);
  private readonly cellsApi = inject(CellService);
  private readonly ref = inject(MatDialogRef<AddMembersDialogComponent, boolean>);
  private readonly destroyRef = inject(DestroyRef);
  readonly data = inject<AddMembersDialogData>(MAT_DIALOG_DATA);

  readonly members = signal<Member[]>([]);
  readonly selected = signal<string[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly search = this.fb.nonNullable.control('');

  constructor() {
    this.search.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load());

    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.membersApi
      .list({
        search: this.search.value,
        unassigned: true,
        page: 1,
        pageSize: 100
      })
      .subscribe({
        next: (result) => {
          this.members.set(result.items.filter((member) => !member.roles.includes('VIP')));
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.loading.set(false);
          this.error.set(apiErrorMessage(error, 'Could not load members.'));
        }
      });
  }

  onSelection(change: MatSelectionListChange): void {
    this.selected.set(change.options.map((option) => option.value as string));
  }

  add(): void {
    if (this.selected().length === 0 || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.error.set('');
    this.cellsApi.addMembers(this.data.cellId, this.selected()).subscribe({
      next: () => {
        this.saving.set(false);
        this.ref.close(true);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(error, 'Could not add these members to the cell.'));
      }
    });
  }

  cancel(): void {
    this.ref.close(false);
  }
}
