import { Directionality } from '@angular/cdk/bidi';
import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  computed,
  inject,
  input,
  linkedSignal,
  viewChildren,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { AbilitySlot } from '../../../../../core/api/generated/models/ability-slot';
import type { ChampionAbility } from '../../../../../core/api/generated/models/champion-ability';
import { tabIndexAfter } from '../../../../../ui/tabs/tab-index-after';
import { tabMoveForKey } from '../../../../../ui/tabs/tab-move-for-key';
import { CatalogueImage } from '../../../shared/cards/catalogue-image';
import { DdragonHtmlPipe } from '../rich-text/ddragon-html-pipe';
import { abilityChipsOf } from './ability-chips-of';
import { abilityKey } from './ability-key';
import { AbilityMedia } from './ability-media';

/** Box of a rail icon, in CSS pixels. */
const TAB_ICON_SIZE = 58;

/**
 * The champion's abilities: a P/Q/W/E/R rail of tabs driving the stage of the selected
 * one, its clip, cast figures and description. Every description is rendered, the hidden
 * ones included, so the server's HTML carries the whole kit. A clip that fails to load
 * leaves its ability to its icon until the next champion.
 */
@Component({
  selector: 'lodb-ability-showcase',
  imports: [AbilityMedia, CatalogueImage, DdragonHtmlPipe, TranslocoPipe],
  templateUrl: './ability-showcase.html',
  styleUrl: './ability-showcase.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AbilityShowcase {
  /** The passive, then the spells in their Q, W, E, R order. */
  readonly abilities = input.required<readonly ChampionAbility[]>();
  /** The champion's resource, as the unit of the costs. */
  readonly resource = input('');

  protected readonly iconSize = TAB_ICON_SIZE;
  protected readonly selected = linkedSignal(() => {
    this.abilities();
    return 0;
  });
  private readonly unavailable = linkedSignal<ReadonlySet<AbilitySlot>>(() => {
    this.abilities();
    return new Set();
  });
  protected readonly panels = computed(() =>
    this.abilities().map((ability) => ({
      ability,
      key: abilityKey(ability.slot),
      chips: abilityChipsOf(ability, this.resource()),
      clip: this.unavailable().has(ability.slot) ? null : (ability.video ?? null),
    })),
  );
  protected readonly current = computed(() => this.panels()[this.selected()] ?? this.panels()[0]);
  private readonly direction = inject(Directionality);
  private readonly tabs = viewChildren<ElementRef<HTMLButtonElement>>('tab');

  protected select(index: number): void {
    this.selected.set(index);
  }

  protected markUnavailable(slot: AbilitySlot): void {
    this.unavailable.update((slots) => new Set([...slots, slot]));
  }

  /** The arrows follow the reading direction and wrap; Home and End reach either end. */
  protected onKeydown(event: KeyboardEvent): void {
    const move = tabMoveForKey(event.key, this.direction.value);
    if (move === null) {
      return;
    }
    event.preventDefault();
    const index = tabIndexAfter(move, this.selected(), this.abilities().length);
    this.select(index);
    this.tabs()[index]?.nativeElement.focus();
  }

  protected tabId(slot: AbilitySlot): string {
    return `ability-tab-${slot}`;
  }

  protected panelId(slot: AbilitySlot): string {
    return `ability-panel-${slot}`;
  }
}
