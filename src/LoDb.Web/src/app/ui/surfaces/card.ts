import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Frame } from './frame';

/**
 * Framed content block with an optional eyebrow and heading. The heading level is the page's
 * business, so the card renders it as an `h3`, the level of a card inside a page section;
 * content that needs another level projects its own heading instead. The `frame` input picks
 * the frame style (plain, interactive, ornate).
 */
@Component({
  selector: 'lodb-card',
  templateUrl: './card.html',
  hostDirectives: [{ directive: Frame, inputs: ['lodbFrame: frame'] }],
  host: { class: 'block p-5' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Card {
  readonly eyebrow = input<string>();
  readonly heading = input<string>();
}
