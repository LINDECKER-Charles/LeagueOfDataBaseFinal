import { Directive, input } from '@angular/core';
import type { Locale } from '../../../../core/i18n/locales';
import { localePath } from '../../../../core/layout/shell/locale-path';
import type { LegalInfo } from '../legal-info';

/**
 * What every legal text receives: the legal parameters it quotes and the page locale its
 * links stay in. A text is its template alone, ported as written from
 * `app/templates/legal/{fr,en}/`; its sections are the anchors of the page's contents.
 */
@Directive({ host: { class: 'contents' } })
export abstract class LegalText {
  readonly info = input.required<LegalInfo>();
  readonly locale = input.required<Locale>();

  protected link(path: string): string {
    return localePath(this.locale(), path);
  }
}
