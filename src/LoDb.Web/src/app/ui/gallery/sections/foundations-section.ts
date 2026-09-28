import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Tokens and global utilities: palette, type, rules, accents, prose and tables. */
@Component({
  selector: 'lodb-gallery-foundations',
  imports: [RouterLink],
  templateUrl: './foundations-section.html',
  host: { class: 'block scroll-mt-20' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FoundationsSection {
  protected readonly palette = [
    'void',
    'abyss',
    'panel',
    'panel-2',
    'panel-raised',
    'gold',
    'gold-bright',
    'gold-light',
    'gold-rich',
    'gold-deep',
    'hex',
    'hex-deep',
    'text',
    'text-muted',
    'text-dim',
    'danger',
    'danger-light',
  ];

  protected swatch(token: string): string {
    return `var(--color-${token})`;
  }
}
