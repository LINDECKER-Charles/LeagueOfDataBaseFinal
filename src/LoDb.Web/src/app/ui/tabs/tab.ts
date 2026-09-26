import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TAB_SELECTION } from './tab-selection-token';

/**
 * One panel of a `lodb-tabs` group. Every panel is rendered and the unselected ones are
 * hidden, so server-rendered content stays indexable. The `key` names the panel in the DOM
 * (`{key}-panel`, `{key}-tab`), so it must be unique in the page.
 */
@Component({
  selector: 'lodb-tab',
  template: '<ng-content />',
  host: {
    class: 'block focus-visible:outline-none',
    role: 'tabpanel',
    tabindex: '0',
    '[id]': 'panelId()',
    '[attr.aria-labelledby]': 'tabId()',
    '[hidden]': '!selected()',
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Tab {
  readonly key = input.required<string>();
  readonly label = input.required<string>();

  private readonly selection = inject(TAB_SELECTION);

  readonly selected = computed(() => this.selection.selectedKey() === this.key());
  readonly panelId = computed(() => `${this.key()}-panel`);
  readonly tabId = computed(() => `${this.key()}-tab`);
}
