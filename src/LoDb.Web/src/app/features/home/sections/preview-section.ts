import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Image } from '../../../ui/media/image';
import { Frame } from '../../../ui/surfaces/frame';
import type { HomeSection } from '../data/home-section';
import type { CardLook } from './card-look';
import { RESOURCE_TEXTS } from './resource-texts';
import { SeeAllArrow } from './see-all-arrow';

// Pixel boxes reserved before the bytes arrive: Data Dragon's square icons are 120px wide,
// its item, spell and rune marks 64px.
const PORTRAIT_SIZE = 120;
const TILE_SIZE = 64;
const INITIALS_LENGTH = 2;

/**
 * One preview of the home: its heading, the size of the whole list, a link to it, and the
 * first entries as cards, each a link to its page in the home's context.
 */
@Component({
  selector: 'lodb-preview-section',
  imports: [Frame, Image, RouterLink, SeeAllArrow, TranslocoPipe],
  templateUrl: './preview-section.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PreviewSection {
  readonly section = input.required<HomeSection>();
  readonly look = input<CardLook>('tile');

  protected readonly texts = computed(() => RESOURCE_TEXTS[this.section().resource]);
  protected readonly portraitSize = PORTRAIT_SIZE;
  protected readonly tileSize = TILE_SIZE;

  protected initialsOf(name: string): string {
    return name.slice(0, INITIALS_LENGTH).toUpperCase();
  }
}
