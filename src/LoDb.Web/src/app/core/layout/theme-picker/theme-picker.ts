import { ChangeDetectionStrategy, Component, Injector, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { THEME_IDENTITIES } from '../../theme/theme-identities';
import { ThemeService } from '../../theme/theme-service';
import { THEMES, type Theme } from '../../theme/themes';
import { ThemeCrest } from './theme-crest';

// Spelt out whole so the stylesheet scan finds them.
const CREST_CLASSES: Readonly<Record<Theme, string>> = {
  hextech: 'theme-trigger__crest--hextech',
  zaun: 'theme-trigger__crest--zaun',
  noxus: 'theme-trigger__crest--noxus',
  'spirit-blossom': 'theme-trigger__crest--spirit-blossom',
};

/**
 * Header trigger of the theme dialog. It renders all four crests and the stylesheet shows
 * the painted identity's: the server HTML stays the same whatever the visitor chose, and
 * the right crest is there from the first frame. Without JavaScript it does nothing, and
 * the served identity is complete: this gates presentation, never content.
 *
 * The dialog and the CDK overlay behind it load on the first press: the trigger sits in the
 * header of every page, the dialog opens on few of them.
 */
@Component({
  selector: 'lodb-theme-picker',
  imports: [ThemeCrest, TranslocoPipe],
  templateUrl: './theme-picker.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ThemePicker {
  protected readonly themes = THEMES;
  protected readonly currentLabel = computed(() => THEME_IDENTITIES[this.service.current()].label);
  private readonly service = inject(ThemeService);
  private readonly injector = inject(Injector);

  protected crestClass(theme: Theme): string {
    return CREST_CLASSES[theme];
  }

  protected async open(): Promise<void> {
    const [{ DialogService }, { ThemeDialog }] = await Promise.all([
      import('../../../ui/overlays/dialog-service'),
      import('./theme-dialog'),
    ]);
    this.injector.get(DialogService).open(ThemeDialog, { labelledBy: ThemeDialog.titleId });
  }
}
