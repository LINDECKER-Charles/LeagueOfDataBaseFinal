import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterRenderEffect,
  inject,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { AdminBand } from './admin-band';
import { AdminNotices } from './admin-notices';

/**
 * Where the outcome of an action shows, at the top of the column, as the legacy flash band
 * did after its redirect. The page does not reload here: the band is brought into view, below
 * the sticky bar, so that an action at the foot of a long table is still answered in sight.
 * A new page clears it, and so does leaving the panels (signing out): the service outlives
 * them, and a band must not greet the next session.
 */
@Component({
  selector: 'lodb-notices-outlet',
  imports: [AdminBand],
  template: `
    @if (notices.current(); as notice) {
      <lodb-admin-band class="scroll-mt-24" [tone]="notice.tone">{{ notice.text }}</lodb-admin-band>
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NoticesOutlet {
  protected readonly notices = inject(AdminNotices);
  private readonly band = viewChild(AdminBand, { read: ElementRef });

  constructor() {
    inject(Router)
      .events.pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.notices.clear());
    inject(DestroyRef).onDestroy(() => this.notices.clear());
    afterRenderEffect(() => {
      if (this.notices.current() !== null) {
        const band = this.band()?.nativeElement as HTMLElement | undefined;
        band?.scrollIntoView?.({ block: 'nearest' });
      }
    });
  }
}
