import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Reveal } from '../../../../../ui/motion/reveal';
import { Frame } from '../../../../../ui/surfaces/frame';
import { CatalogueImage } from '../../../../../ui/cards/catalogue-image';
import { RichText } from '../../../shared/codex/rich-text/rich-text';
import type { Constellation } from './constellation';

/** Box of a keystone's icon, in CSS pixels. */
const KEYSTONE_ICON_SIZE = 92;
/** Box of a minor rune's medallion, in CSS pixels. */
const MEDALLION_SIZE = 48;

/**
 * The signature of a rune path page: its keystones and minor rows strung on a spine of
 * light, tinted by the path's `--path` colour. Each rune card carries the anchor the list's
 * cards link to.
 */
@Component({
  selector: 'lodb-rune-constellation',
  imports: [CatalogueImage, Frame, Reveal, RichText, TranslocoPipe],
  templateUrl: './rune-constellation.html',
  styleUrl: './rune-constellation.css',
  host: { class: 'constellation block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RuneConstellation {
  readonly constellation = input.required<Constellation>();

  protected readonly keystoneIconSize = KEYSTONE_ICON_SIZE;
  protected readonly medallionSize = MEDALLION_SIZE;
}
