import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * Localised placeholder of the home page, proving SSR, routing and i18n end to end. It lives
 * outside `features/`, which the workspace chantier does not own; L3.1 replaces it with the
 * `home` feature.
 */
@Component({
  selector: 'lodb-hello-page',
  imports: [TranslocoPipe],
  templateUrl: './hello-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HelloPage {}
