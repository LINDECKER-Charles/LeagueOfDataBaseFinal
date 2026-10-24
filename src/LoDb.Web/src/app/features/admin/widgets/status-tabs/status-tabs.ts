import { Directionality } from '@angular/cdk/bidi';
import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  computed,
  inject,
  input,
  model,
  viewChildren,
} from '@angular/core';
import { tabIndexAfter } from '../../../../ui/tabs/tab-index-after';
import { tabMoveForKey } from '../../../../ui/tabs/tab-move-for-key';
import type { TabSelection } from '../../../../ui/tabs/tab-selection';
import { TAB_SELECTION } from '../../../../ui/tabs/tab-selection-token';
import type { StatusTile } from './status-tile';

// Spelled out whole for the Tailwind scanner: a key figure's tile that is also a tab.
const TILE = [
  'tile relative flex min-w-0 cursor-pointer flex-col items-start gap-1 border px-4 py-3',
  'text-start transition-colors focus-visible:outline-1 focus-visible:outline-offset-2',
  'focus-visible:outline-hex motion-reduce:transition-none',
].join(' ');
const IDLE = `${TILE} border-gold/28 bg-void/60 text-text-muted hover:border-hex/70`;
const SELECTED = `${TILE} border-gold bg-gold/8 text-gold-bright`;

/**
 * The views of a page as a group of tabs whose tabs are key figures (`StatusTile`): each tile
 * names its view, gives its figure and what qualifies it, a dot when it has something to
 * say, so that the tab strip is also the page's summary. The ARIA tabs pattern with automatic
 * activation, as `lodb-tabs`: one tab stop, the arrows (mirrored right to left), Home and
 * End; its panels are `lodb-tab` elements whose keys are the tiles' keys. Two tiles a row on
 * a phone, three on a tablet, all of them in a row on a wide screen.
 */
@Component({
  selector: 'lodb-status-tabs',
  template: `
    <div
      role="tablist"
      class="grid grid-cols-2 gap-2 sm:grid-cols-3 xl:grid-cols-6"
      [attr.aria-label]="label()"
    >
      @for (tile of tiles(); track tile.key; let index = $index) {
        @let isSelected = tile.key === selectedKey();
        <button
          #tabButton
          type="button"
          role="tab"
          [class]="isSelected ? selectedClass : idleClass"
          [id]="tile.key + '-tab'"
          [attr.aria-controls]="tile.key + '-panel'"
          [attr.aria-selected]="isSelected"
          [attr.aria-labelledby]="tile.key + '-tab-name'"
          [attr.aria-describedby]="tile.key + '-tab-state'"
          [attr.data-view]="tile.key"
          [tabIndex]="isSelected ? 0 : -1"
          (click)="selected.set(tile.key)"
          (keydown)="onKeydown($event, index)"
        >
          <span class="flex w-full min-w-0 items-center gap-2">
            <span
              class="min-w-0 truncate text-[0.66rem] tracking-[0.14em] uppercase"
              data-tile-name
              [id]="tile.key + '-tab-name'"
              >{{ tile.label }}</span
            >
            @if (tile.tone) {
              <span class="dot" aria-hidden="true" [attr.data-tone]="tile.tone"></span>
            }
          </span>
          <span class="flex min-w-0 flex-col gap-0.5" [id]="tile.key + '-tab-state'">
            <span
              class="font-beaufort text-[1.4rem] leading-tight text-gold-bright"
              data-tile-value
              >{{ tile.value }}</span
            >
            @if (tile.sub) {
              <span class="text-[0.72rem] leading-snug text-text-dim max-sm:hidden" data-tile-sub>{{
                tile.sub
              }}</span>
            }
          </span>
        </button>
      }
    </div>
    <div class="pt-5">
      <ng-content />
    </div>
  `,
  styles: `
    /* The selected tile reads as the tab of the panel under it: a gold rule along its foot. */
    .tile[aria-selected='true']::after {
      content: '';
      position: absolute;
      inset-inline: -1px;
      inset-block-end: -1px;
      block-size: 2px;
      background: var(--color-gold);
    }
    .dot {
      inline-size: 0.45rem;
      block-size: 0.45rem;
      flex: none;
      margin-inline-start: auto;
      border-radius: 50%;
      background: var(--color-text-dim);
    }
    .dot[data-tone='live'] {
      background: var(--color-hex);
      animation: tile-pulse 2s ease-in-out infinite;
    }
    .dot[data-tone='good'] {
      background: var(--color-good);
    }
    .dot[data-tone='warn'] {
      background: var(--color-gold);
    }
    .dot[data-tone='bad'] {
      background: var(--color-bad);
    }
    @keyframes tile-pulse {
      50% {
        box-shadow: 0 0 0 0.3rem color-mix(in srgb, var(--color-hex) 25%, transparent);
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .dot[data-tone='live'] {
        animation: none;
      }
    }
  `,
  providers: [{ provide: TAB_SELECTION, useExisting: StatusTabs }],
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusTabs implements TabSelection {
  /** Accessible name of the tab list, translated. */
  readonly label = input.required<string>();
  readonly tiles = input.required<readonly StatusTile[]>();
  /** The key of the tile chosen; the first tile while it names none of them. */
  readonly selected = model<string | null>(null);

  private readonly buttons = viewChildren<ElementRef<HTMLButtonElement>>('tabButton');
  private readonly direction = inject(Directionality);

  protected readonly idleClass = IDLE;
  protected readonly selectedClass = SELECTED;

  readonly selectedKey = computed(() => {
    const keys = this.tiles().map((tile) => tile.key);
    const chosen = this.selected();
    return chosen !== null && keys.includes(chosen) ? chosen : keys[0];
  });

  protected onKeydown(event: KeyboardEvent, index: number): void {
    const move = tabMoveForKey(event.key, this.direction.valueSignal());
    const tiles = this.tiles();
    if (move === null || tiles.length === 0) {
      return;
    }
    event.preventDefault();
    const target = tabIndexAfter(move, index, tiles.length);
    this.selected.set(tiles[target].key);
    this.buttons()[target]?.nativeElement.focus();
  }
}
