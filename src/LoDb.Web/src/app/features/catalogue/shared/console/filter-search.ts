import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  booleanAttribute,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { SEARCH_SHORTCUT_KEY } from '../search/search-shortcut-key';
import { CatalogueFilter } from '../state/catalogue-filter';

/**
 * The live search of a list, drawn in the rail (with its `/` hint) and in the mobile bar.
 * Every keystroke filters; the list reaches the field through `focus()` for the shortcut.
 */
@Component({
  selector: 'lodb-filter-search',
  template: `<label class="search">
    <svg class="search__icon" viewBox="0 0 20 20" fill="none" aria-hidden="true">
      <circle cx="9" cy="9" r="6" stroke="currentColor" stroke-width="1.7" />
      <path d="M14 14l4 4" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" />
    </svg>
    <input
      type="search"
      class="search__input"
      enterkeyhint="search"
      [placeholder]="label()"
      [attr.aria-label]="label()"
      [value]="filter.state().query"
      (input)="filter.setQuery(field.value)"
      #field
    />
    @if (hint()) {
      <kbd class="search__kbd" aria-hidden="true">{{ shortcut }}</kbd>
    }
  </label>`,
  styleUrl: './filter-search.css',
  host: { class: 'block min-w-0' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FilterSearch {
  /** Placeholder and accessible name, such as "Search for an item…". */
  readonly label = input.required<string>();
  /** Shows the `/` hint: the rail only, the mobile bar has no keyboard. */
  readonly hint = input(false, { transform: booleanAttribute });

  protected readonly filter = inject(CatalogueFilter);
  protected readonly shortcut = SEARCH_SHORTCUT_KEY;
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');

  /** Whether the field is laid out: the rail and the bar never show at once. */
  isShown(): boolean {
    return this.field().nativeElement.getClientRects().length > 0;
  }

  focus(): void {
    const field = this.field().nativeElement;
    field.focus();
    field.select();
  }
}
