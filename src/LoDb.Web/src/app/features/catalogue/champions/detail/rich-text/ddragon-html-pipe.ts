import { Pipe, type PipeTransform, inject } from '@angular/core';
import { DomSanitizer, type SafeHtml } from '@angular/platform-browser';
import { richTextHtml } from '../../../shared/codex/rich-text/rich-text-html';

/**
 * Data Dragon rich text for `[innerHTML]`, with its own vocabulary kept (`<magicDamage>`),
 * which Angular's sanitizer would strip: richTextHtml has already reduced it to the tags it
 * allows, without attributes but a checked colour, so it is trusted as is. Styled by
 * `.ddragon-rich`.
 */
@Pipe({ name: 'ddragonHtml' })
export class DdragonHtmlPipe implements PipeTransform {
  private readonly sanitizer = inject(DomSanitizer);

  transform(raw: string | null | undefined): SafeHtml {
    return this.sanitizer.bypassSecurityTrustHtml(richTextHtml(raw ?? ''));
  }
}
