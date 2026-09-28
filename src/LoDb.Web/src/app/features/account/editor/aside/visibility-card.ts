import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { Field } from '../../../../ui/controls/field';

/**
 * Whether visitors see the public card, a switch saved on its own; a public profile shows
 * the address of its card.
 */
@Component({
  selector: 'lodb-visibility-card',
  imports: [Field, RouterLink, TranslocoPipe],
  templateUrl: './visibility-card.html',
  styleUrl: './aside.css',
  host: {
    class: 'visibility-card',
    '[class.visibility-card--public]': 'isPublic()',
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VisibilityCard {
  private readonly page = inject(PageDirection);

  readonly isPublic = input.required<boolean>();
  readonly username = input.required<string>();
  readonly changed = output<boolean>();

  protected readonly publicPath = computed(() => `/u/${this.username()}`);
  protected readonly publicLink = computed(() =>
    localePath(this.page.locale(), `u/${encodeURIComponent(this.username())}`),
  );
}
