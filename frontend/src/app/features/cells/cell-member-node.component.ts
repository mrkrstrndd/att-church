import { Component, Input, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CellService } from '../../core/services/cell.service';
import { CellDetail, CellListItem, CellMember } from '../../core/models/cell.model';

@Component({
  selector: 'app-cell-member-node',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './cell-member-node.component.html',
  styleUrl: './cell-member-node.component.scss'
})
export class CellMemberNodeComponent {
  private readonly cellsApi = inject(CellService);

  @Input({ required: true }) member!: CellMember;
  @Input() cells: CellListItem[] = [];
  @Input() ancestorIds: string[] = [];

  readonly expanded = signal(false);
  readonly loading = signal(false);
  readonly detail = signal<CellDetail | null>(null);
  readonly error = signal('');

  get ledCell(): CellListItem | null {
    return this.cells.find((cell) => cell.leaderMemberId === this.member.id) ?? null;
  }

  get canExpand(): boolean {
    return !!this.ledCell && this.ledCell.memberCount > 0 && !this.ancestorIds.includes(this.member.id);
  }

  get childAncestorIds(): string[] {
    return [...this.ancestorIds, this.member.id];
  }

  toggle(): void {
    if (!this.canExpand) {
      return;
    }

    if (this.expanded()) {
      this.expanded.set(false);
      return;
    }

    this.expanded.set(true);
    if (this.detail() || this.loading()) {
      return;
    }

    const ledCell = this.ledCell;
    if (!ledCell) {
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.cellsApi.get(ledCell.id).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Could not load this leader’s members.');
      }
    });
  }

  initials(name: string): string {
    return name
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part.charAt(0))
      .join('')
      .toUpperCase();
  }

  roleClass(role: string): string {
    return `role-chip role-chip--${role.toLowerCase()}`;
  }
}
