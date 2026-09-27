import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { PageDirection } from '../../core/layout/direction/page-direction';
import { localePath } from '../../core/layout/shell/locale-path';
import { Button } from '../controls/button';
import { FRAME_STYLES } from './frame-styles';

/**
 * The framed "no results" of a view that holds nothing, the legacy site's empty state: a
 * line saying so, what is missing when the caller knows it, then a way back and a way home.
 */
@Component({
  selector: 'lodb-empty-state',
  imports: [Button, RouterLink, TranslocoPipe],
  template: `<p class="mb-3 eyebrow">{{ 'common.no_result.text' | transloco }}</p>
    @if (text(); as line) {
      <p class="mx-auto max-w-md text-sm text-text-muted">{{ line }}</p>
    }
    <div class="mt-6 flex items-center justify-center gap-3">
      <button type="button" lodbButton="ghost" (click)="back()">
        {{ 'common.no_result.back' | transloco }}
      </button>
      <a lodbButton [routerLink]="home()">{{ 'header.navigation.home' | transloco }}</a>
    </div>`,
  host: { class: `${FRAME_STYLES.plain} block p-10 text-center` },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmptyState {
  /** What is missing, already translated; the bare "no results" line when left out. */
  readonly text = input<string | null>(null);

  private readonly location = inject(Location);
  private readonly page = inject(PageDirection);

  protected readonly home = computed(() => localePath(this.page.locale(), ''));

  protected back(): void {
    this.location.back();
  }
}
