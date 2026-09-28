import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { IconName } from './icon-name';

/**
 * Inline SVG glyph on the header's icon grammar (24px box, currentColor stroke), sized by the
 * classes of the host. Always decorative: the control carrying it holds the accessible name.
 * Global styles size it through the `hx-icon` host class: its inner SVG fills the host with a
 * utility, which no component-layer rule could override.
 */
@Component({
  selector: 'lodb-icon',
  templateUrl: './icon.html',
  host: { class: 'hx-icon inline-block shrink-0', 'aria-hidden': 'true' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Icon {
  readonly name = input.required<IconName>();
}
