import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/**
 * One group of facets under its heading, after the ARIA accordion pattern: a heading holding
 * a button that reports and toggles the group, and a labelled region that stays in the DOM
 * while folded. The heading carries the group's mark, lit once one of its facets filters,
 * and how many do. Its owner decides whether it is open, so a group can stay unfolded when
 * its last facet is cleared under the pointer.
 */
@Component({
  selector: 'lodb-facet-group',
  template: `<div role="heading" aria-level="3">
      <button
        type="button"
        class="title"
        [id]="groupId() + '-head'"
        [attr.aria-expanded]="open()"
        [attr.aria-controls]="groupId() + '-body'"
        (click)="toggled.emit()"
      >
        <span class="marker" [class.marker--on]="engaged() > 0" aria-hidden="true"></span>
        <span class="name">{{ name() }}</span>
        @if (engaged() > 0) {
          <b class="badge">{{ engaged() }}</b>
        }
        <svg
          class="chevron"
          [class.chevron--open]="open()"
          viewBox="0 0 20 20"
          fill="currentColor"
          aria-hidden="true"
        >
          <path
            fill-rule="evenodd"
            clip-rule="evenodd"
            d="M5.23 7.21a.75.75 0 011.06.02L10 10.94l3.71-3.71a.75.75 0 111.06 1.06l-4.24 4.24a.75.75 0 01-1.06 0L5.21 8.29a.75.75 0 01.02-1.08z"
          />
        </svg>
      </button>
    </div>
    <div
      role="region"
      class="body"
      [id]="groupId() + '-body'"
      [attr.aria-labelledby]="groupId() + '-head'"
      [hidden]="!open()"
    >
      <ng-content />
    </div>`,
  styles: `
    /* A group sits flush with the search: no inline padding, so the chips wrap where they did. */
    :host {
      display: block;
      border-block-start: 1px solid color-mix(in srgb, var(--color-gold-deep) 35%, transparent);
    }
    .title {
      display: flex;
      align-items: center;
      gap: 0.55rem;
      inline-size: 100%;
      padding-block: 0.7rem 0.55rem;
      padding-inline: 0;
      text-align: start;
      border: 0;
      background: none;
      cursor: pointer;
    }
    .title:focus-visible {
      outline: 1px solid var(--color-hex);
      outline-offset: 2px;
    }
    /* Named .marker, the class each identity reshapes in its ornament sheet. */
    .marker {
      inline-size: 7px;
      block-size: 7px;
      flex: none;
      rotate: 45deg;
      border: 1px solid var(--color-gold-deep);
      background: transparent;
      transition:
        border-color 0.2s var(--ease-hextech),
        background-color 0.2s var(--ease-hextech);
    }
    .marker--on {
      border-color: var(--color-hex);
      background: var(--color-hex);
      box-shadow: 0 0 8px color-mix(in srgb, var(--color-hex) 80%, transparent);
    }
    .name {
      font-family: var(--font-beaufort);
      font-size: 0.75rem;
      text-transform: uppercase;
      letter-spacing: 0.14em;
      color: var(--color-gold);
    }
    .badge {
      display: grid;
      place-items: center;
      min-inline-size: 1.05rem;
      block-size: 1.05rem;
      padding-inline: 0.2rem;
      font-family: var(--font-mono);
      font-size: 0.62rem;
      font-weight: 400;
      color: var(--color-hextech-black);
      background: var(--color-hex);
    }
    .chevron {
      margin-inline-start: auto;
      inline-size: 14px;
      block-size: 14px;
      flex: none;
      color: var(--color-text-dim);
      transition: transform 0.25s var(--ease-hextech);
    }
    .chevron--open {
      transform: rotate(180deg);
    }
    .body {
      display: flex;
      flex-direction: column;
      gap: 0.8rem;
      padding-block: 0.1rem 0.9rem;
    }
    .body[hidden] {
      display: none;
    }
    @media (prefers-reduced-motion: reduce) {
      .marker,
      .chevron {
        transition: none;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FacetGroup {
  readonly name = input.required<string>();
  /** How many of its facets filter the list. */
  readonly engaged = input(0);
  readonly open = input.required<boolean>();
  /**
   * Base of the heading and region ids. The rail and the sheet are both in the DOM while the
   * sheet is open, so it names the surface as well as the group.
   */
  readonly groupId = input.required<string>();
  /** The reader asked to fold or unfold the group. */
  readonly toggled = output();
}
