import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { VipService } from '../../core/services/vip.service';
import { VipReport, VipReportItem } from '../../core/models/vip.model';
import { apiErrorMessage, formatDateTime } from '../../core/utils';

@Component({
  selector: 'app-vip',
  standalone: true,
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSnackBarModule,
    MatTableModule,
    MatTooltipModule,
    PageHeaderComponent,
    EmptyStateComponent
  ],
  templateUrl: './vip.component.html',
  styleUrl: './vip.component.scss'
})
export class VipComponent implements OnInit {
  private readonly vipApi = inject(VipService);
  private readonly snackBar = inject(MatSnackBar);

  readonly report = signal<VipReport | null>(null);
  readonly loading = signal(true);
  readonly searchTerm = signal('');
  readonly displayedColumns = ['vip', 'contact', 'invitedBy', 'signIns', 'progress', 'joined'];

  readonly filteredMembers = computed<VipReportItem[]>(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const members = this.report()?.members ?? [];
    if (!term) {
      return members;
    }
    return members.filter(
      (member) =>
        member.fullName.toLowerCase().includes(term) ||
        member.email.toLowerCase().includes(term) ||
        (member.phoneNo ?? '').toLowerCase().includes(term) ||
        (member.invitedByName ?? '').toLowerCase().includes(term)
    );
  });

  readonly nearPromotion = computed(
    () => this.report()?.members.filter((member) => member.signInCount === 3).length ?? 0
  );

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.vipApi.report().subscribe({
      next: (report) => {
        this.report.set(report);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.snackBar.open(apiErrorMessage(error, 'Could not load the VIP report.'), 'Close', {
          duration: 5000
        });
      }
    });
  }

  onSearch(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
  }

  joinedLabel(value: string): string {
    return formatDateTime(value);
  }

  initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part.charAt(0))
      .join('')
      .toUpperCase();
  }

  progress(count: number): number {
    return Math.min(100, Math.round((count / 4) * 100));
  }

  exportCsv(): void {
    const report = this.report();
    if (!report || report.members.length === 0) {
      return;
    }

    const header = ['VIP name', 'Email', 'Phone', 'Invited by', 'Sign-ins', 'VIP since'];
    const rows = report.members.map((member) => [
      member.fullName,
      member.email,
      member.phoneNo ?? '',
      member.invitedByName ?? 'Walk-in',
      member.signInCount.toString(),
      this.joinedLabel(member.joinedAt)
    ]);

    const csv = [header, ...rows]
      .map((row) => row.map((cell) => this.csvCell(cell)).join(','))
      .join('\r\n');

    const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `identity-church-vip-report-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  private csvCell(value: string): string {
    return `"${value.replace(/"/g, '""')}"`;
  }
}
