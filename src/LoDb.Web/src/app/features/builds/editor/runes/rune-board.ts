import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RuneOption } from '../../../../core/api/generated/models/rune-option';
import type { RuneTreeOption } from '../../../../core/api/generated/models/rune-tree-option';
import { Image } from '../../../../ui/media/image';
import { CatalogState } from '../catalog/catalog-state';
import { EditorCatalogs } from '../catalog/editor-catalogs';
import { imageSource } from '../shared/image-source';
import { initialsOf } from '../shared/initials-of';
import { RUNE_LIMITS } from './rune-limits';
import { RuneEditing } from './rune-editing';

/**
 * The rune page: the primary trees, then the four rows of the one picked, the keystone
 * first; then the other trees for the secondary side and their minor rows, where the game
 * client's two-rows rule applies. A stored rune the patch lacks shows as a ghost chip: its
 * author decides whether to replace it. Each tree tints the board with its own colour.
 */
@Component({
  selector: 'lodb-rune-board',
  imports: [CatalogState, Image, TranslocoPipe],
  templateUrl: './rune-board.html',
  styleUrl: './rune-board.css',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RuneBoard {
  protected readonly editing = inject(RuneEditing);
  protected readonly runes = inject(EditorCatalogs).runes;
  protected readonly page = this.editing.page;
  protected readonly imageSource = imageSource;
  protected readonly initialsOf = initialsOf;
  protected readonly keystoneSlot = RUNE_LIMITS.keystoneSlot;

  protected readonly primaryTree = computed(() => this.treeOf(this.page().primaryStyleId));
  protected readonly secondaryTree = computed(() => this.treeOf(this.page().secondaryStyleId));
  protected readonly secondaryChoices = computed(() =>
    (this.runes.options() ?? []).filter((tree) => tree.id !== this.page().primaryStyleId),
  );

  /** The class that tints a tree's controls with its colour (`precision`, `domination`...). */
  protected pathClass(tree: RuneTreeOption | null): string {
    return tree ? `path-${tree.key.toLowerCase()}` : '';
  }

  protected titleOf(rune: RuneOption): string {
    return rune.shortDesc ? `${rune.name} — ${rune.shortDesc}` : rune.name;
  }

  /** The stored rune of a primary row that the row does not offer on this patch. */
  protected primaryGhost(slotIndex: number, row: readonly RuneOption[]): number | null {
    const picked = this.page().primaryPerks[slotIndex] ?? null;
    return picked === null || row.some((rune) => rune.id === picked) ? null : picked;
  }

  private treeOf(styleId: number | null): RuneTreeOption | null {
    return (this.runes.options() ?? []).find((tree) => tree.id === styleId) ?? null;
  }
}
