import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { NgxEchartsDirective } from 'ngx-echarts';
import type { EChartsOption } from 'echarts';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { DashboardService } from '../../core/services/dashboard.service';
import { AuthService } from '../../core/services/auth.service';
import {
  AttendanceByCell,
  DashboardKpis,
  MonthlyComparison,
  RecentEvent,
  WeeklyTrend
} from '../../core/models/dashboard.model';
import { apiErrorMessage, formatDate, toIsoDate } from '../../core/utils';

interface KpiCard {
  label: string;
  value: string;
  icon: string;
  tone: string;
  note: string;
  delta: number | null;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSnackBarModule,
    NgxEchartsDirective,
    EmptyStateComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private readonly dashboard = inject(DashboardService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);

  readonly firstName = computed(() => this.auth.user()?.firstName?.trim() || 'friend');

  readonly loading = signal(true);
  readonly kpis = signal<DashboardKpis | null>(null);
  readonly weekly = signal<WeeklyTrend | null>(null);
  readonly byCell = signal<AttendanceByCell | null>(null);
  readonly monthly = signal<MonthlyComparison | null>(null);
  readonly recent = signal<RecentEvent[]>([]);

  readonly rangeForm = this.fb.nonNullable.group({
    from: [daysAgo(89), [Validators.required]],
    to: [new Date(), [Validators.required]]
  });

  readonly cards = computed<KpiCard[]>(() => {
    const kpi = this.kpis();
    return [
      {
        label: 'Active members',
        value: kpi ? `${kpi.totalActiveMembers}` : '—',
        icon: 'groups',
        tone: 'orange',
        note: 'Members currently walking with us',
        delta: null
      },
      {
        label: 'Cells',
        value: kpi ? `${kpi.totalCells}` : '—',
        icon: 'hub',
        tone: 'amber',
        note: 'Small groups caring for one another',
        delta: null
      },
      {
        label: 'Events this month',
        value: kpi ? `${kpi.eventsThisMonth}` : '—',
        icon: 'event',
        tone: 'terracotta',
        note: 'Scheduled gatherings',
        delta: kpi?.eventsThisMonthDeltaPercent ?? null
      },
      {
        label: 'Last event attendance',
        value: kpi ? `${kpi.lastEvent?.attendance ?? 0}` : '—',
        icon: 'how_to_reg',
        tone: 'clay',
        note: kpi?.lastEvent ? `${kpi.lastEvent.name} · ${formatDate(kpi.lastEvent.date)}` : 'No event recorded yet',
        delta: null
      },
      {
        label: 'Average attendance',
        value: kpi ? `${kpi.averageAttendance.toFixed(1)}` : '—',
        icon: 'insights',
        tone: 'apricot',
        note: 'Average per event day in range',
        delta: kpi?.averageAttendanceDeltaPercent ?? null
      },
      {
        label: 'Attendance rate',
        value: kpi ? `${kpi.attendanceRatePercent.toFixed(1)}%` : '—',
        icon: 'donut_large',
        tone: 'rose',
        note: 'Last event attendance vs. active members',
        delta: null
      }
    ];
  });

  readonly weeklyOptions = computed<EChartsOption>(() => {
    const points = this.weekly()?.points ?? [];
    return {
      grid: { left: 12, right: 18, top: 24, bottom: 12, containLabel: true },
      tooltip: { trigger: 'axis', valueFormatter: (value) => `${value} attendances` },
      xAxis: {
        type: 'category',
        boundaryGap: false,
        data: points.map((point) => point.label),
        axisLine: { lineStyle: { color: '#ead7c8' } },
        axisLabel: { color: '#8b7163', fontSize: 11 },
        axisTick: { show: false }
      },
      yAxis: {
        type: 'value',
        minInterval: 1,
        splitLine: { lineStyle: { color: 'rgba(91,53,34,0.08)' } },
        axisLabel: { color: '#8b7163', fontSize: 11 }
      },
      series: [
        {
          name: 'Attendance',
          type: 'line',
          smooth: true,
          symbol: 'circle',
          symbolSize: 7,
          data: points.map((point) => point.totalAttendance),
          lineStyle: { width: 3, color: '#e8751a' },
          itemStyle: { color: '#e8751a', borderColor: '#ffffff', borderWidth: 2 },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0,
              y: 0,
              x2: 0,
              y2: 1,
              colorStops: [
                { offset: 0, color: 'rgba(232,117,26,0.3)' },
                { offset: 1, color: 'rgba(232,117,26,0.01)' }
              ]
            }
          }
        }
      ]
    };
  });

  readonly cellOptions = computed<EChartsOption>(() => {
    const cells = [...(this.byCell()?.cells ?? [])].reverse();
    return {
      grid: { left: 12, right: 38, top: 16, bottom: 12, containLabel: true },
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
      xAxis: {
        type: 'value',
        minInterval: 1,
        splitLine: { lineStyle: { color: 'rgba(91,53,34,0.08)' } },
        axisLabel: { color: '#8b7163', fontSize: 11 }
      },
      yAxis: {
        type: 'category',
        data: cells.map((cell) => cell.name),
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: '#5d4031', fontSize: 11 }
      },
      series: [
        {
          name: 'Attendance',
          type: 'bar',
          barWidth: 16,
          data: cells.map((cell) => cell.attendanceCount),
          itemStyle: {
            borderRadius: [0, 8, 8, 0],
            color: {
              type: 'linear',
              x: 0,
              y: 0,
              x2: 1,
              y2: 0,
              colorStops: [
                { offset: 0, color: '#f6ad72' },
                { offset: 1, color: '#e8751a' }
              ]
            }
          },
          label: { show: true, position: 'right', color: '#5d4031', fontSize: 11 }
        }
      ]
    };
  });

  readonly monthlyOptions = computed<EChartsOption>(() => {
    const monthly = this.monthly();
    return {
      grid: { left: 12, right: 18, top: 42, bottom: 12, containLabel: true },
      tooltip: { trigger: 'axis' },
      legend: {
        top: 0,
        right: 0,
        icon: 'roundRect',
        textStyle: { color: '#8b7163', fontSize: 11 }
      },
      xAxis: {
        type: 'category',
        data: monthly?.labels ?? [],
        axisLine: { lineStyle: { color: '#ead7c8' } },
        axisTick: { show: false },
        axisLabel: { color: '#8b7163', fontSize: 11 }
      },
      yAxis: {
        type: 'value',
        minInterval: 1,
        splitLine: { lineStyle: { color: 'rgba(91,53,34,0.08)' } },
        axisLabel: { color: '#8b7163', fontSize: 11 }
      },
      series: [
        {
          name: 'Total attendance',
          type: 'bar',
          barMaxWidth: 22,
          data: monthly?.totalAttendance ?? [],
          itemStyle: { color: '#e8751a', borderRadius: [6, 6, 0, 0] }
        },
        {
          name: 'Unique attendees',
          type: 'line',
          smooth: true,
          symbol: 'circle',
          symbolSize: 6,
          data: monthly?.uniqueAttendees ?? [],
          lineStyle: { color: '#b9510c', width: 2 },
          itemStyle: { color: '#b9510c' }
        }
      ]
    };
  });

  ngOnInit(): void {
    this.load();
  }

  applyRange(): void {
    const { from, to } = this.rangeForm.getRawValue();
    if (from > to) {
      this.snackBar.open('The start date must be before the end date.', 'Close', { duration: 4000 });
      return;
    }
    this.load();
  }

  load(): void {
    const { from, to } = this.rangeForm.getRawValue();
    this.loading.set(true);

    forkJoin({
      kpis: this.dashboard.kpis(toIsoDate(from), toIsoDate(to)),
      weekly: this.dashboard.weeklyTrend(12),
      byCell: this.dashboard.attendanceByCell(toIsoDate(from), toIsoDate(to)),
      monthly: this.dashboard.monthlyComparison(6),
      recent: this.dashboard.recentEvents(6)
    }).subscribe({
      next: (data) => {
        this.kpis.set(data.kpis);
        this.weekly.set(data.weekly);
        this.byCell.set(data.byCell);
        this.monthly.set(data.monthly);
        this.recent.set(data.recent);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.snackBar.open(apiErrorMessage(error, 'Could not load dashboard data.'), 'Close', {
          duration: 5000
        });
      }
    });
  }

  deltaLabel(delta: number | null): string {
    if (delta === null) {
      return '';
    }
    const arrow = delta >= 0 ? '▲' : '▼';
    return `${arrow} ${Math.abs(delta).toFixed(1)}%`;
  }

  dayOfMonth(value: string): string {
    return value.slice(8, 10);
  }

  monthShort(value: string): string {
    const date = new Date(`${value}T00:00:00`);
    return Number.isNaN(date.getTime())
      ? ''
      : new Intl.DateTimeFormat('en-US', { month: 'short' }).format(date);
  }
}

function daysAgo(days: number): Date {
  const date = new Date();
  date.setDate(date.getDate() - days);
  date.setHours(0, 0, 0, 0);
  return date;
}
