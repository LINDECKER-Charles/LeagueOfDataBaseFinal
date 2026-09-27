import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Image } from '../../../../ui/media/image';
import { CatalogState } from '../catalog/catalog-state';
import { EditorCatalogs } from '../catalog/editor-catalogs';
import { imageSource } from '../shared/image-source';
import { initialsOf } from '../shared/initials-of';
import { SearchIndex } from '../shared/search-index';
import { STEP_LIMITS } from '../steps/order/step-limits';
import { StepEditing } from '../steps/step-editing';
import type { ArmoryData } from './armory-data';
import { ITEM_CATEGORIES, type ItemCategory } from './item-categories';
import { matchesCategory } from './matches-category';

/**
 * The armory of a step: the whole item list of the patch and mode, narrowed by a search
 * and a category. A click adds the item and the armory stays open, counting what it added
 * and badging what the step already holds, so a step is composed in one pass. A dialog, a
 * bottom sheet on a phone. It is the pane itself rather than a lodb-dialog frame: its head,
 * tools and footer stay pinned while the grid alone scrolls, as the legacy armory did.
 */
@Component({
  selector: 'lodb-item-armory',
  imports: [CatalogState, Image, TranslocoPipe],
  templateUrl: './item-armory.html',
  styleUrl: './item-armory.css',
  host: { class: 'hx-dialog-panel armory' },
  providers: [
    { provide: StepEditing, useFactory: () => inject<ArmoryData>(DIALOG_DATA).editing },
    { provide: EditorCatalogs, useFactory: () => inject<ArmoryData>(DIALOG_DATA).catalogs },
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ItemArmory {
  /** Id of the armory's heading, which names the dialog: the opener's `labelledBy`. */
  static readonly HEADING_ID = 'lodb-item-armory-heading';

  private readonly ref = inject(DialogRef);
  private readonly editing = inject(StepEditing);
  protected readonly step = inject<ArmoryData>(DIALOG_DATA).step;
  protected readonly items = inject(EditorCatalogs).items;
  protected readonly headingId = ItemArmory.HEADING_ID;
  protected readonly categories = ITEM_CATEGORIES;
  protected readonly maxItems = STEP_LIMITS.maxItemsPerStep;
  protected readonly imageSource = imageSource;
  protected readonly initialsOf = initialsOf;
  protected readonly query = signal('');
  protected readonly category = signal<ItemCategory>('all');
  /** What this opening added, the counter of the footer. */
  protected readonly added = signal(0);

  private readonly index = computed(
    () => new SearchIndex(this.items.options() ?? [], (item) => `${item.name} ${item.id}`),
  );
  protected readonly shown = computed(() =>
    this.index().matching(this.query(), (item) => matchesCategory(item, this.category())),
  );
  protected readonly placed = computed(() => this.editing.order().steps[this.step]?.items ?? []);
  protected readonly canAdd = computed(() => this.editing.order().canAddItem(this.step));
  protected readonly target = computed(() => {
    const label = this.editing.order().steps[this.step]?.label.trim() ?? '';
    const position = this.step + 1;
    return label === '' ? `${position}` : `${position}. ${label}`;
  });

  /** How many of an item the step already holds: the same item may be bought twice. */
  protected countInStep(itemId: string): number {
    return this.placed().filter((placed) => placed === itemId).length;
  }

  protected add(itemId: string): void {
    if (this.canAdd()) {
      this.editing.appendItem(this.step, itemId);
      this.added.update((count) => count + 1);
    }
  }

  protected close(): void {
    this.ref.close();
  }
}
