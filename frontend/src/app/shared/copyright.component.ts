import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-copyright',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="credit">
      <img src="phase-space-mark.png" alt="" aria-hidden="true" />
      <span>© {{ year }} Phase and Space Software Solutions.</span>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
        width: 100%;
      }

      .credit {
        display: flex;
        align-items: center;
        justify-content: flex-end;
        gap: 0.45rem;
        padding: 0.8rem clamp(1rem, 3vw, 2.5rem);
        color: var(--church-muted);
        font-size: 0.65rem;
        font-weight: 500;
        letter-spacing: 0.015em;
        line-height: 1.3;
        opacity: 0.88;
      }

      img {
        width: auto;
        height: 1.2rem;
        opacity: 0.82;
      }

      @media (max-width: 600px) {
        .credit {
          justify-content: flex-end;
          padding: 0.7rem 1rem;
          font-size: 0.6rem;
        }

        img {
          height: 1rem;
        }
      }
    `
  ]
})
export class CopyrightComponent {
  readonly year = new Date().getFullYear();
}
