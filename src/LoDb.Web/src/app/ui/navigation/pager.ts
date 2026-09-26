import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Icon } from '../media/icon';
import type { PagerLink } from './pager-link';

/**
 * The codex turning its own pages: the previous and next entity in collection order, with a
 * hub linking back to the list. A missing neighbour leaves an empty cell, so the hub never
 * moves. The links carry `rel` so the order is machine-readable too.
 */
@Component({
  selector: 'lodb-pager',
  imports: [Icon, RouterLink, TranslocoPipe],
  templateUrl: './pager.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Pager {
  readonly previous = input<PagerLink | null>(null);
  readonly next = input<PagerLink | null>(null);
  /** The list the entity belongs to; its name is not shown, the hub is labelled generically. */
  readonly hub = input.required<string>();
}
