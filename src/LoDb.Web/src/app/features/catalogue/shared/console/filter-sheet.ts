import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Button } from '../../../../ui/controls/button';
import { CatalogueFilter } from '../state/catalogue-filter';
import { FacetPanel } from './facets/facet-panel';

/**
 * The facets as a bottom sheet on narrow screens. The dialog lives at the root of the page,
 * out of the list's injector: the list hands its filter over as the dialog's data, and the
 * sheet provides it again to the facets. Filtering stays live behind the sheet; its call to
 * action only closes it, telling how many results wait.
 */
@Component({
  selector: 'lodb-filter-sheet',
  imports: [Button, FacetPanel, TranslocoPipe],
  templateUrl: './filter-sheet.html',
  styleUrls: ['./marks.css', './filter-sheet.css'],
  providers: [{ provide: CatalogueFilter, useFactory: () => inject(DIALOG_DATA) }],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FilterSheet {
  /** Id of the sheet's heading, which names the dialog: the opener's `labelledBy`. */
  static readonly HEADING_ID = 'lodb-filter-sheet-heading';

  protected readonly filter = inject(CatalogueFilter);
  protected readonly headingId = FilterSheet.HEADING_ID;
  private readonly ref = inject(DialogRef);

  protected close(): void {
    this.ref.close();
  }
}
