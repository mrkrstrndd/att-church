import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { forkJoin } from 'rxjs';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../shared/confirm-dialog.component';
import { CellFormDialogComponent, CellDialogData } from './cell-form-dialog.component';
import { AddMembersDialogComponent, AddMembersDialogData } from './add-members-dialog.component';
import { CellService } from '../../core/services/cell.service';
import { AuthService } from '../../core/services/auth.service';
import { CellDetail, CellListItem, CellMember } from '../../core/models/cell.model';
import { apiErrorMessage, formatDate } from '../../core/utils';

@Component({
  selector: 'app-cell-detail',
  standalone: true,
  imports: [
    RouterLink,
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    MatSnackBarModule,
    MatTableModule,
    MatTooltipModule,
    PageHeaderComponent,
    EmptyStateComponent
  ],
  templateUrl: './cell-detail.component.html',
  styleUrl: './cell-detail.component.scss'
})
export class CellDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly cellsApi = inject(CellService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  readonly auth = inject(AuthService);

  readonly cell = signal<CellDetail | null>(null);
  readonly allCells = signal<CellListItem[]>([]);
  readonly loading = signal(true);
  readonly displayedColumns = ['member', 'contact', 'roles', 'actions'];
  private readonly cellId = this.route.snapshot.paramMap.get('id') ?? '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    forkJoin({
      cell: this.cellsApi.get(this.cellId),
      cells: this.cellsApi.list()
    }).subscribe({
      next: ({ cell, cells }) => {
        this.cell.set(cell);
        this.allCells.set(cells);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.snackBar.open(apiErrorMessage(error, 'Could not load this cell.'), 'Close', {
          duration: 5000
        });
      }
    });
  }

  get leader(): CellMember | undefined {
    return this.cell()?.members.find((member) => member.isLeader);
  }

  memberInitials(member: CellMember): string {
    return member.fullName
      .split(' ')
      .slice(0, 2)
      .map((part) => part.charAt(0))
      .join('')
      .toUpperCase();
  }

  editCell(): void {
    const cell = this.cell();
    if (!cell) {
      return;
    }
    const listItem = this.allCells().find((item) => item.id === cell.id);
    if (!listItem) {
      return;
    }

    const data: CellDialogData = { cell: listItem, cells: this.allCells() };
    this.dialog
      .open(CellFormDialogComponent, { data, width: '600px', maxWidth: '94vw' })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.snackBar.open('Cell updated.', 'Close', { duration: 3500 });
          this.load();
        }
      });
  }

  addMembers(): void {
    const cell = this.cell();
    if (!cell) {
      return;
    }
    const data: AddMembersDialogData = {
      cellId: cell.id,
      cellName: cell.name,
      existingMemberIds: cell.members.map((member) => member.id)
    };
    this.dialog
      .open(AddMembersDialogComponent, { data, width: '560px', maxWidth: '94vw' })
      .afterClosed()
      .subscribe((added) => {
        if (added) {
          this.snackBar.open('Members added to the cell.', 'Close', { duration: 3500 });
          this.load();
        }
      });
  }

  removeMember(member: CellMember): void {
    const cell = this.cell();
    if (!cell || member.isLeader) {
      return;
    }
    const data: ConfirmDialogData = {
      title: 'Remove member from cell?',
      message: `${member.fullName} will become unassigned. Their attendance history is not affected.`,
      confirmLabel: 'Remove',
      destructive: true
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '420px', maxWidth: '92vw' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.cellsApi.removeMember(cell.id, member.id).subscribe({
          next: () => {
            this.snackBar.open(`${member.fullName} was removed from the cell.`, 'Close', {
              duration: 3500
            });
            this.load();
          },
          error: (error: unknown) =>
            this.snackBar.open(apiErrorMessage(error, 'Could not remove this member.'), 'Close', {
              duration: 5000
            })
        });
      });
  }

  deleteCell(): void {
    const cell = this.cell();
    if (!cell) {
      return;
    }
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
          next: () => void this.router.navigate(['/cells']),
          error: (error: unknown) =>
            this.snackBar.open(apiErrorMessage(error, 'Could not deactivate this cell.'), 'Close', {
              duration: 5000
            })
        });
      });
  }

  roleClass(role: string): string {
    return `role-chip role-chip--${role.toLowerCase()}`;
  }

  createdLabel(): string {
    return formatDate(this.cell()?.createdAt.slice(0, 10));
  }
}
