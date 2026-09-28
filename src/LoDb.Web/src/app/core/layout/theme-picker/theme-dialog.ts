import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { DialogFrame } from '../../../ui/overlays/dialog-frame';
import { THEME_IDENTITIES } from '../../theme/theme-identities';
import { ThemeService } from '../../theme/theme-service';
import { THEMES, type Theme } from '../../theme/themes';
import { ThemeCrest } from './theme-crest';

// Spelt out whole so the stylesheet scan finds them; the themes declare the swatch variables.
const SWATCH_CLASSES: Readonly<Record<Theme, string>> = {
  hextech: 'theme-swatch--hextech',
  zaun: 'theme-swatch--zaun',
  noxus: 'theme-swatch--noxus',
  'spirit-blossom': 'theme-swatch--spirit-blossom',
};

/**
 * The four identities side by side, each on its own ground with its crest, name, city and a
 * strip of its palette, so the choice is made on what a theme looks like, not on a word.
 */
@Component({
  selector: 'lodb-theme-dialog',
  imports: [DialogFrame, ThemeCrest, TranslocoPipe],
  templateUrl: './theme-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ThemeDialog {
  /** Id of the heading, which names the dialog and its group of options. */
  static readonly titleId = 'theme-dialog-title';

  protected readonly themes = THEMES;
  protected readonly identities = THEME_IDENTITIES;
  protected readonly titleId = ThemeDialog.titleId;
  protected readonly service = inject(ThemeService);
  private readonly ref = inject(DialogRef);

  protected swatchClass(theme: Theme): string {
    return SWATCH_CLASSES[theme];
  }

  protected choose(theme: Theme): void {
    this.service.select(theme);
    this.ref.close();
  }
}
