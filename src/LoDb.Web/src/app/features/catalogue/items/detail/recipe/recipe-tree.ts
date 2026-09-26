import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { PageContext } from '../../../../../core/context/page-context';
import { CatalogueImage } from '../../../shared/cards/catalogue-image';
import { injectCatalogueLink } from '../../../shared/codex/links/inject-catalogue-link';
import type { RecipeNode } from './recipe-node';

/** Box of a node's icon, in CSS pixels. */
const ICON_SIZE = 44;

/**
 * The crafting tree of an item, drawn top-down with connector lines: the item, lit gold,
 * then its components, each a link to its own page. A wide tree scrolls from its start.
 */
@Component({
  selector: 'lodb-recipe-tree',
  imports: [CatalogueImage, NgTemplateOutlet, RouterLink],
  templateUrl: './recipe-tree.html',
  styleUrl: './recipe-tree.css',
  host: { class: 'block overflow-x-auto pb-2' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecipeTree {
  readonly tree = input.required<RecipeNode>();
  readonly context = input.required<PageContext>();

  protected readonly iconSize = ICON_SIZE;
  protected readonly linkOf = injectCatalogueLink();
}
