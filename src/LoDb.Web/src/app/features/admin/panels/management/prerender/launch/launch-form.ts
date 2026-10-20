import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  input,
  output,
  viewChild,
} from '@angular/core';
import { LOCALES } from '../../../../../../core/i18n/locales';
import type { PassRequest } from '../../../../../../core/api/generated/models/pass-request';
import { AdminCard } from '../../../../layout/admin-card';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../shared/inject-admin-text';
import { ConfirmButton } from '../../../../widgets/confirm-button';
import { passRequestOf } from './pass-request-of';

/** The types of page a pass may keep to, as the API and the URLs name them. */
const PAGE_TYPES = ['champions', 'items', 'runes', 'summoners'] as const;

/**
 * Why no pass may be launched now: one is already active, or the pre-rendering is not
 * configured on this host. A key under `admin.prerender.launch`.
 */
export type LaunchLock = 'locked_active' | 'locked_dormant';

/**
 * The launch of a pass, the spec's form: the epoch it writes (the served one unless a newer
 * one replaces it), its perimeter (a range of versions, a type of page, a locale) and the
 * versions to render first, pasted from the edge's log. Every field may stay empty. It asks
 * twice, then hands the request to the panel, which runs it.
 */
@Component({
  selector: 'lodb-launch-form',
  imports: [AdminCard, AdminTextPipe, ConfirmButton],
  templateUrl: './launch-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LaunchForm {
  /** The epoch nginx serves, which a pass writes unless told otherwise. */
  readonly servedEpoch = input.required<number>();
  /** Whether a patch leaving the latest rank joins the pass by itself. */
  readonly enqueuesPromotions = input.required<boolean>();
  /** Why the launch is refused now; null when it is open. */
  readonly lock = input<LaunchLock | null>(null);
  /** Whether an action of the panel is in flight. */
  readonly busy = input(false);
  readonly launched = output<PassRequest>();

  protected readonly texts = injectAdminText();
  protected readonly pageTypes = PAGE_TYPES;
  protected readonly locales = LOCALES;

  private readonly form = viewChild.required<ElementRef<HTMLFormElement>>('form');

  /** Enter in a field must not launch anything: the launch always asks twice. */
  protected hold(event: Event): void {
    event.preventDefault();
  }

  protected launch(): void {
    this.launched.emit(passRequestOf(new FormData(this.form().nativeElement)));
  }
}
