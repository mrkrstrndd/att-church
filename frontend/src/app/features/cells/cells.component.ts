import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../shared/confirm-dialog.component';
import { CellFormDialogComponent, CellDialogData } from './cell-form-dialog.component';
import { CellService } from '../../core/services/cell.service';
import { AuthService } from '../../core/services/auth.service';
import { CellDetail, CellListItem, CellMember } from '../../core/models/cell.model';
import { apiErrorMessage } from '../../core/utils';

@Component({
  selector: 'app-cells',
  standalone: true,
  imports: [
    RouterLink,
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
    PageHeaderComponent,
    EmptyStateComponent
  ],
  templateUrl: './cells.component.html',
  styleUrl: './cells.component.scss'
})
export class CellsComponent implements OnInit {
  private readonly cellsApi = inject(CellService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  readonly auth = inject(AuthService);

  readonly cells = signal<CellListItem[]>([]);
  readonly loading = signal(true);
  readonly expandedCellId = signal<string | null>(null);
  readonly expandedLeaderMemberId = signal<string | null>(null);
  readonly details = signal<Record<string, CellDetail>>({});
  readonly detailLoadingId = signal<string | null>(null);
  readonly nestedLoadingCellId = signal<string | null>(null);
  readonly totalMembers = computed(() => this.cells().reduce((sum, cell) => sum + cell.memberCount, 0));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.cellsApi.list().subscribe({
      next: (cells) => {
        this.cells.set(cells);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.snackBar.open(apiErrorMessage(error, 'Could not load cells.'), 'Close', {
          duration: 5000
        });
      }
    });
  }

  toggleCell(cell: CellListItem): void {
    this.expandedLeaderMemberId.set(null);
    if (this.expandedCellId() === cell.id) {
      this.expandedCellId.set(null);
      return;
    }

    this.expandedCellId.set(cell.id);
    if (this.details()[cell.id]) {
      return;
    }

    this.detailLoadingId.set(cell.id);
    this.cellsApi.get(cell.id).subscribe({
      next: (detail) => {
        this.details.update((current) => ({ ...current, [cell.id]: detail }));
        this.detailLoadingId.set(null);
      },
      error: (error: unknown) => {
        this.detailLoadingId.set(null);
        this.snackBar.open(apiErrorMessage(error, 'Could not load the members for this cell.'), 'Close', {
          duration: 5000
        });
      }
    });
  }

  leaderCellFor(member: CellMember): CellListItem | null {
    return this.cells().find((cell) => cell.leaderMemberId === member.id) ?? null;
  }

  toggleLeaderMembers(member: CellMember): void {
    const ledCell = this.leaderCellFor(member);
    if (!ledCell) {
      return;
    }

    if (this.expandedLeaderMemberId() === member.id) {
      this.expandedLeaderMemberId.set(null);
      return;
    }

    this.expandedLeaderMemberId.set(member.id);
    if (this.details()[ledCell.id]) {
      return;
    }

    this.nestedLoadingCellId.set(ledCell.id);
    this.cellsApi.get(ledCell.id).subscribe({
      next: (detail) => {
        this.details.update((current) => ({ ...current, [ledCell.id]: detail }));
        this.nestedLoadingCellId.set(null);
      },
      error: (error: unknown) => {
        this.nestedLoadingCellId.set(null);
        this.snackBar.open(apiErrorMessage(error, "Could not load this leader's members."), 'Close', {
          duration: 5000
        });
      }
    });
  }

  detailFor(cellId: string): CellDetail | null {
    return this.details()[cellId] ?? null;
  }

  memberInitials(member: CellMember): string {
    return member.fullName
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part.charAt(0))
      .join('')
      .toUpperCase();
  }

  roleClass(role: string): string {
    return `role-chip role-chip--${role.toLowerCase()}`;
  }

  createCell(): void {
    const data: CellDialogData = { cells: this.cells() };
    this.dialog
      .open(CellFormDialogComponent, { data, width: '600px', maxWidth: '94vw' })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.snackBar.open('Cell created.', 'Close', { duration: 3500 });
          this.load();
        }
      });
  }

  deleteCell(cell: CellListItem): void {
    const data: ConfirmDialogData = {
      title: 'Deactivate cell?',
      message: `${cell.name} will be archived and its members will become unassigned. Historical attendance remains intact.`,
      confirmLabel: 'Deactivate',
      destructive: true
    };

    this.dialog
      .open(ConfirmDialogComponent, { data, width: '420px', maxWidth: '92vw' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.cellsApi.delete(cell.id).subscribe({
          next: () => {
            this.snackBar.open(`${cell.name} was deactivated.`, 'Close', { duration: 3500 });
            if (this.expandedCellId() === cell.id) {
              this.expandedCellId.set(null);
              this.expandedLeaderMemberId.set(null);
            }
            this.load();
          },
          error: (error: unknown) =>
            this.snackBar.open(apiErrorMessage(error, 'Could not deactivate this cell.'), 'Close', {
              duration: 5000
            })
        });
      });
  }
}
