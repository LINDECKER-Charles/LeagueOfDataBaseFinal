import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  booleanAttribute,
  inject,
  input,
  linkedSignal,
  viewChild,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { SEARCH_SHORTCUT_KEY } from '../search/search-shortcut-key';
import { CatalogueFilter } from '../state/catalogue-filter';

/**
 * The live search of a list, drawn in the rail (with its `/` hint) and in the mobile bar.
 * Every keystroke filters; the list reaches the field through `focus()` for the shortcut.
 * Focusing it reads the whole list, so it is there by the first keystroke.
 * The placeholder is the short "Search…" of every list, which fits the narrow rail; the
 * accessible name says what the list holds.
 */
@Component({
  selector: 'lodb-filter-search',
  imports: [TranslocoPipe],
  template: `<label class="search">
    <svg class="search__icon" viewBox="0 0 20 20" fill="none" aria-hidden="true">
      <circle cx="9" cy="9" r="6" stroke="currentColor" stroke-width="1.7" />
      <path d="M14 14l4 4" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" />
    </svg>
    <input
      type="search"
      class="search__input"
      [class.search__input--hinted]="hint()"
      enterkeyhint="search"
      [placeholder]="'common.search' | transloco"
      [attr.aria-label]="label()"
      [value]="text()"
      (focus)="filter.anticipate()"
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
  /** Accessible name, such as "Search for an item…". */
  readonly label = input.required<string>();
  /** Shows the `/` hint: the rail only, the mobile bar has no keyboard. */
  readonly hint = input(false, { transform: booleanAttribute });

  protected readonly filter = inject(CatalogueFilter);
  protected readonly shortcut = SEARCH_SHORTCUT_KEY;
  /** What the field shows: the state's query, once the state has heard what was typed. */
  protected readonly text = linkedSignal(() => this.filter.state().query);
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');

  constructor() {
    // Hydrated after the reader started typing (the rail's console waits for them), the
    // field already holds their text: drawing the state's query would erase it before the
    // replayed keystrokes hand it over. A field the browser builds itself is empty here.
    const typed =
      inject<ElementRef<HTMLElement>>(ElementRef).nativeElement.querySelector('input')?.value;
    if (typed !== undefined && typed !== '') {
      this.text.set(typed);
    }
  }

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
