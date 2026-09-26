import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { richTextHtml } from './rich-text-html';

/**
 * A description in Riot's rich text, coloured by foundation/ddragon.css. The markup is
 * rebuilt from an allow-list first (richTextHtml), which is what makes trusting it safe.
 */
@Component({
  selector: 'lodb-rich-text',
  template: '',
  host: { class: 'ddragon-rich block', '[innerHTML]': 'html()' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RichText {
  /** The API's text: Riot's markup, template tokens already removed. */
  readonly text = input.required<string>();

  private readonly sanitizer = inject(DomSanitizer);

  protected readonly html = computed(() =>
    this.sanitizer.bypassSecurityTrustHtml(richTextHtml(this.text())),
  );
}
