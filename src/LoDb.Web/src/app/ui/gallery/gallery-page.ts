import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { Reveal } from '../motion/reveal';
import type { SectionLink } from '../navigation/section-link';
import { SectionNav } from '../navigation/section-nav';
import { Backdrop } from '../surfaces/backdrop';
import { GALLERY_STRINGS } from './gallery-strings';
import { ControlsSection } from './sections/controls-section';
import { FoundationsSection } from './sections/foundations-section';
import { OverlaysSection } from './sections/overlays-section';
import { StructureSection } from './sections/structure-section';
import { SurfacesSection } from './sections/surfaces-section';

/**
 * Development gallery: every primitive of the design system on one page, in the painted
 * theme (header picker) and the locale's direction (`/ar/dev/gallery` is right-to-left).
 * The gallery-probe tool walks it through the four themes in both directions.
 */
@Component({
  selector: 'lodb-gallery-page',
  imports: [
    Backdrop,
    ControlsSection,
    FoundationsSection,
    OverlaysSection,
    Reveal,
    SectionNav,
    StructureSection,
    SurfacesSection,
  ],
  templateUrl: './gallery-page.html',
  host: { class: 'relative isolate block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GalleryPage {
  protected readonly sections: readonly SectionLink[] = [
    { id: 'foundations', label: 'Foundations' },
    { id: 'controls', label: 'Controls' },
    { id: 'surfaces', label: 'Surfaces' },
    { id: 'overlays', label: 'Overlays' },
    { id: 'structure', label: 'Structure' },
  ];

  constructor() {
    const transloco = inject(TranslocoService);
    for (const [locale, strings] of Object.entries(GALLERY_STRINGS)) {
      transloco.setTranslation(strings, locale);
    }
  }
}
