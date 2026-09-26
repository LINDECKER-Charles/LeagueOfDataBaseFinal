import { Pipe, type PipeTransform, inject } from '@angular/core';
import { DomSanitizer, type SafeHtml } from '@angular/platform-browser';
import { ddragonHtml } from './ddragon-html';

/**
 * Data Dragon rich text for `[innerHTML]`, with its own vocabulary kept (`<magicDamage>`),
 * which Angular's sanitizer would strip: ddragonHtml has already reduced it to tags without
 * attributes, so it is trusted as is. Styled by `.ddragon-rich`.
 */
@Pipe({ name: 'ddragonHtml' })
export class DdragonHtmlPipe implements PipeTransform {
  private readonly sanitizer = inject(DomSanitizer);

  transform(raw: string | null | undefined): SafeHtml {
    return this.sanitizer.bypassSecurityTrustHtml(ddragonHtml(raw ?? ''));
  }
}
