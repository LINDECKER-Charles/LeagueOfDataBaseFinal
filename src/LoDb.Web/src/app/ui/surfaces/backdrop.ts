import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Ambient page backdrop: two glows, a woven texture and a vignette, laid under a `relative`
 * page container. The weave is the one piece of atmosphere the tokens cannot theme, because
 * the four identities need a different geometry, not a different colour: all four patterns
 * ship and the theme shows one (a hidden `<rect>` is never rasterised). One per page, as the
 * pattern ids are document-wide.
 */
@Component({
  selector: 'lodb-backdrop',
  templateUrl: './backdrop.html',
  host: {
    class: 'pointer-events-none absolute inset-0 -z-10 overflow-hidden',
    'aria-hidden': 'true',
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Backdrop {}
