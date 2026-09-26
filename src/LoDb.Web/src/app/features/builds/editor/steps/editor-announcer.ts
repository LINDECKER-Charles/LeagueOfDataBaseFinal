import { LiveAnnouncer } from '@angular/cdk/a11y';
import { Injectable, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

/**
 * Tells screen readers, politely, what a move of the purchase order did: a drag and its
 * buttons read alike. The CDK's announcer clears its region first, so the same sentence
 * twice in a row is heard twice.
 */
@Injectable()
export class EditorAnnouncer {
  private readonly live = inject(LiveAnnouncer);
  private readonly transloco = inject(TranslocoService);

  say(key: string, params: Readonly<Record<string, number>> = {}): void {
    void this.live.announce(this.transloco.translate(key, params), 'polite');
  }
}
