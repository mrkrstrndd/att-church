import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../shared/confirm-dialog.component';
import { EventFormDialogComponent, EventDialogData } from './event-form-dialog.component';
import { EventService } from '../../core/services/event.service';
import { AuthService } from '../../core/services/auth.service';
import { EventListItem, EventMonth } from '../../core/models/event.model';
import { apiErrorMessage, formatDate } from '../../core/utils';

interface EventMonthGroup {
  key: string;
  label: string;
  events: EventListItem[];
}

@Component({
  selector: 'app-events',
  standalone: true,
  imports: [
    RouterLink,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSnackBarModule,
    MatTableModule,
    MatTooltipModule,
    PageHeaderComponent,
    EmptyStateComponent
  ],
  templateUrl: './events.component.html',
  styleUrl: './events.component.scss'
})
export class EventsComponent implements OnInit {
  private readonly eventsApi = inject(EventService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  readonly auth = inject(AuthService);

  readonly events = signal<EventListItem[]>([]);
  readonly months = signal<EventMonth[]>([]);
  readonly selectedMonth = signal('');
  readonly loading = signal(true);
  readonly total = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);
  readonly displayedColumns = ['event', 'dates', 'type', 'attendance', 'actions'];
  readonly groups = computed<EventMonthGroup[]>(() => this.groupByMonth(this.events()));

  ngOnInit(): void {
    this.loadMonths();
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.eventsApi.list(this.pageIndex() + 1, this.pageSize(), this.selectedMonth()).subscribe({
      next: (result) => {
        this.events.set(result.items);
        this.total.set(result.totalCount);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.snackBar.open(apiErrorMessage(error, 'Could not load events.'), 'Close', {
          duration: 5000
        });
      }
    });
  }

  loadMonths(): void {
    this.eventsApi.months().subscribe({
      next: (months) => this.months.set(months),
      error: () => this.months.set([])
    });
  }

  onMonthChange(month: string): void {
    this.selectedMonth.set(month);
    this.pageIndex.set(0);
    this.load();
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  monthLabel(month: string): string {
    const [year, monthNumber] = month.split('-').map(Number);
    if (!year || !monthNumber) {
      return month;
    }
    return new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(
      new Date(year, monthNumber - 1, 1)
    );
  }

  createEvent(): void {
    this.openDialog({});
  }

  editEvent(item: EventListItem): void {
    this.eventsApi.get(item.id).subscribe({
      next: (event) => this.openDialog({ event }),
      error: (error: unknown) =>
        this.snackBar.open(apiErrorMessage(error, 'Could not load this event.'), 'Close', {
          duration: 5000
        })
    });
  }

  deleteEvent(item: EventListItem): void {
    const firstConfirm: ConfirmDialogData = {
      title: 'Delete event?',
      message: `${item.name} and all of its event days will be removed. If attendance exists, you will be asked to confirm again.`,
      confirmLabel: 'Delete event',
      destructive: true
    };
    this.dialog
      .open(ConfirmDialogComponent, { data: firstConfirm, width: '440px', maxWidth: '92vw' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.eventsApi.delete(item.id, false).subscribe({
          next: () => this.afterDelete(item),
          error: (error: unknown) => {
            if (error instanceof HttpErrorResponse && error.status === 409) {
              this.confirmForceDelete(item, apiErrorMessage(error));
              return;
            }
            this.snackBar.open(apiErrorMessage(error, 'Could not delete this event.'), 'Close', {
              duration: 5000
            });
          }
        });
      });
  }

  dateLabel(item: EventListItem): string {
    return item.isMultiDay
      ? `${formatDate(item.startDate)} – ${formatDate(item.endDate)}`
      : formatDate(item.startDate);
  }

  canCheckIn(): boolean {
    return this.auth.isAdmin() || this.auth.isCellLeader();
  }

  private confirmForceDelete(item: EventListItem, message: string): void {
    const data: ConfirmDialogData = {
      title: 'Attendance history will be deleted',
      message,
      confirmLabel: 'Delete anyway',
      destructive: true
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '460px', maxWidth: '92vw' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.eventsApi.delete(item.id, true).subscribe({
          next: () => this.afterDelete(item),
          error: (error: unknown) =>
            this.snackBar.open(apiErrorMessage(error, 'Could not delete this event.'), 'Close', {
              duration: 5000
            })
        });
      });
  }

  private afterDelete(item: EventListItem): void {
    this.snackBar.open(`${item.name} was deleted.`, 'Close', { duration: 3500 });
    this.refreshAll();
  }

  private refreshAll(): void {
    this.loadMonths();
    this.load();
  }

  private groupByMonth(events: EventListItem[]): EventMonthGroup[] {
    const groups = new Map<string, EventListItem[]>();
    for (const event of events) {
      const key = event.startDate.slice(0, 7);
      const group = groups.get(key);
      if (group) {
        group.push(event);
      } else {
        groups.set(key, [event]);
      }
    }

    return [...groups.entries()].map(([key, items]) => ({
      key,
      label: this.monthLabel(key),
      events: items
    }));
  }

  private openDialog(data: EventDialogData): void {
    this.dialog
      .open(EventFormDialogComponent, { data, width: '640px', maxWidth: '94vw' })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.snackBar.open(data.event ? 'Event updated.' : 'Event created.', 'Close', {
            duration: 3500
          });
          this.refreshAll();
        }
      });
  }
}
