import { Directive, TemplateRef, inject, input } from '@angular/core';
import type { CatalogueListSource } from '../source/catalogue-list-source';
import type { CatalogueListLike } from '../state/catalogue-list-like';
import type { CatalogueCardContext } from './catalogue-card-context';

/**
 * The card of a list, as its page draws it, inside lodb-catalogue-list:
 * `<lodb-entity-card *lodbCatalogueCard="let card of list" ... />`. The source only types
 * the card; the list decides which cards are drawn.
 */
@Directive({ selector: '[lodbCatalogueCard]' })
export class CatalogueCardTemplate<C> {
  readonly template = inject<TemplateRef<CatalogueCardContext<C>>>(TemplateRef);
  readonly lodbCatalogueCardOf = input<CatalogueListSource<CatalogueListLike<C>>>();

  static ngTemplateContextGuard<C>(
    _directive: CatalogueCardTemplate<C>,
    _context: unknown,
  ): _context is CatalogueCardContext<C> {
    return true;
  }
}
