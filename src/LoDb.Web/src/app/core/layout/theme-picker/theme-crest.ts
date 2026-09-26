import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { Theme } from '../../theme/themes';

/**
 * Identity crests, one idea each, on the header's icon grammar: the hex crystal and its gem
 * facet (Hextech), a valve wheel (Zaun), a blade between two rising barbs (Noxus), a blossom
 * around a lit core (Spirit Blossom). Sized by the host's classes.
 */
@Component({
  selector: 'lodb-theme-crest',
  templateUrl: './theme-crest.html',
  host: { 'aria-hidden': 'true' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ThemeCrest {
  readonly theme = input.required<Theme>();
}
