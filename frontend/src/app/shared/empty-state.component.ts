import { Component, Input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [MatIconModule],
  template: `
    <div class="empty-state">
      <div class="empty-state__icon"><mat-icon>{{ icon }}</mat-icon></div>
      <h3>{{ title }}</h3>
      <p>{{ message }}</p>
    </div>
  `,
  styles: [
    `
      .empty-state {
        padding: 3rem 1.25rem;
        text-align: center;
        color: var(--church-muted);
      }

      .empty-state__icon {
        display: grid;
        width: 3.25rem;
        height: 3.25rem;
        margin: 0 auto 0.9rem;
        border-radius: 50%;
        background: var(--church-sand);
        color: var(--church-gold-dark);
        place-items: center;
      }

      h3 {
        margin: 0 0 0.4rem;
        color: var(--church-ink);
        font-size: 1rem;
      }

      p {
        max-width: 42ch;
        margin: 0 auto;
        font-size: 0.9rem;
        line-height: 1.55;
      }
    `
  ]
})
export class EmptyStateComponent {
  @Input() icon = 'inbox';
  @Input({ required: true }) title = '';
  @Input() message = '';
}
