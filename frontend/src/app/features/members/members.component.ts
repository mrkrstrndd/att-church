import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../shared/confirm-dialog.component';
import { MemberFormDialogComponent, MemberDialogData } from './member-form-dialog.component';
import { MemberService } from '../../core/services/member.service';
import { CellService } from '../../core/services/cell.service';
import { AuthService } from '../../core/services/auth.service';
import { Member } from '../../core/models/member.model';
import { CellListItem } from '../../core/models/cell.model';
import { Role } from '../../core/models/auth.model';
import { apiErrorMessage } from '../../core/utils';

@Component({
  selector: 'app-members',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSnackBarModule,
    MatTableModule,
    MatTooltipModule,
    PageHeaderComponent,
    EmptyStateComponent
  ],
  templateUrl: './members.component.html',
  styleUrl: './members.component.scss'
})
export class MembersComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly membersApi = inject(MemberService);
  private readonly cellsApi = inject(CellService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  readonly auth = inject(AuthService);

  readonly members = signal<Member[]>([]);
  readonly cells = signal<CellListItem[]>([]);
  readonly loading = signal(true);
  readonly total = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);

  readonly searchControl = this.fb.nonNullable.control('');
  readonly roleControl = this.fb.nonNullable.control<Role | ''>('');
  readonly cellControl = this.fb.nonNullable.control('');

  readonly displayedColumns = ['member', 'contact', 'roles', 'cell', 'login', 'actions'];
  readonly roleOptions: Role[] = ['Member', 'VIP', 'CellLeader', 'Admin'];

  constructor() {
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.pageIndex.set(0);
        this.load();
      });

    this.roleControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.pageIndex.set(0);
      this.load();
    });

    this.cellControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.pageIndex.set(0);
      this.load();
    });
  }

  ngOnInit(): void {
    this.loadCells();
    this.load();
  }

  loadCells(): void {
    this.cellsApi.list().subscribe({
      next: (cells) => this.cells.set(cells),
      error: () => this.cells.set([])
    });
  }

  load(): void {
    this.loading.set(true);
    this.membersApi
      .list({
        search: this.searchControl.value,
        role: this.roleControl.value,
        cellId: this.cellControl.value || null,
        page: this.pageIndex() + 1,
        pageSize: this.pageSize()
      })
      .subscribe({
        next: (result) => {
          this.members.set(result.items);
          this.total.set(result.totalCount);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.loading.set(false);
          this.snackBar.open(apiErrorMessage(error, 'Could not load members.'), 'Close', {
            duration: 5000
          });
        }
      });
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  clearFilters(): void {
    this.searchControl.setValue('', { emitEvent: false });
    this.roleControl.setValue('', { emitEvent: false });
    this.cellControl.setValue('', { emitEvent: false });
    this.pageIndex.set(0);
    this.load();
  }

  addMember(): void {
    this.openDialog({ cells: this.cells() });
  }

  editMember(member: Member): void {
    this.openDialog({ member, cells: this.cells() });
  }

  deleteMember(member: Member): void {
    const data: ConfirmDialogData = {
      title: 'Deactivate member?',
      message: `${member.fullName} will be removed from the active directory. Historical attendance is preserved.`,
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
        this.membersApi.delete(member.id).subscribe({
          next: () => {
            this.snackBar.open(`${member.fullName} was deactivated.`, 'Close', { duration: 3500 });
            this.load();
            this.loadCells();
          },
          error: (error: unknown) =>
            this.snackBar.open(apiErrorMessage(error, 'Could not deactivate this member.'), 'Close', {
              duration: 5000
            })
        });
      });
  }

  initials(member: Member): string {
    return `${member.firstName.charAt(0)}${member.lastName.charAt(0)}`.toUpperCase();
  }

  roleClass(role: Role): string {
    return `role-chip role-chip--${role.toLowerCase()}`;
  }

  private openDialog(data: MemberDialogData): void {
    this.dialog
      .open(MemberFormDialogComponent, {
        data,
        width: '720px',
        maxWidth: '94vw',
        autoFocus: 'first-tabbable'
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.snackBar.open(data.member ? 'Member updated.' : 'Member added.', 'Close', {
            duration: 3500
          });
          this.load();
          this.loadCells();
        }
      });
  }
}
