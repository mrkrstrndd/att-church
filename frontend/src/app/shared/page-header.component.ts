import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-page-header',
  standalone: true,
  template: `
    <header class="page-header">
      <div class="page-header__copy">
        @if (eyebrow) {
          <p class="eyebrow">{{ eyebrow }}</p>
        }
        <h1>{{ title }}</h1>
        @if (subtitle) {
          <p class="subtitle">{{ subtitle }}</p>
        }
      </div>
      <div class="page-header__actions">
        <ng-content />
      </div>
    </header>
  `,
  styles: [
    `
      .page-header {
        display: flex;
        align-items: flex-end;
        justify-content: space-between;
        gap: 1rem;
        margin-bottom: 1.5rem;
      }

      .page-header__copy h1 {
        margin: 0;
        font-family: var(--church-display);
        font-size: clamp(1.65rem, 2.3vw, 2.2rem);
        font-weight: 700;
        letter-spacing: -0.03em;
        color: var(--church-ink);
      }

      .eyebrow {
        margin: 0 0 0.35rem;
        color: var(--church-gold-dark);
        font-size: 0.72rem;
        font-weight: 700;
        letter-spacing: 0.16em;
        text-transform: uppercase;
      }

      .subtitle {
        max-width: 56ch;
        margin: 0.35rem 0 0;
        color: var(--church-muted);
        font-size: 0.92rem;
        line-height: 1.55;
      }

      .page-header__actions {
        display: flex;
        flex-wrap: wrap;
        justify-content: flex-end;
        gap: 0.6rem;
      }

      @media (max-width: 720px) {
        .page-header {
          align-items: flex-start;
          flex-direction: column;
        }

        .page-header__actions {
          width: 100%;
          justify-content: flex-start;
        }
      }
    `
  ]
})
export class PageHeaderComponent {
  @Input({ required: true }) title = '';
  @Input() subtitle = '';
  @Input() eyebrow = '';
}
