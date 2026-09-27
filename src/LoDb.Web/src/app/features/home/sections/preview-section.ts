import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink, type UrlTree } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { EntityCard } from '../../../ui/cards/entity-card';
import { Image } from '../../../ui/media/image';
import { Frame } from '../../../ui/surfaces/frame';
import type { HomeLink } from '../data/home-link';
import type { HomeSection } from '../data/home-section';
import type { CardLook } from './card-look';
import { RESOURCE_TEXTS } from './resource-texts';
import { SeeAllArrow } from './see-all-arrow';

// Pixel box reserved before the bytes arrive: Data Dragon's square icons are 120px wide.
const PORTRAIT_SIZE = 120;
const INITIALS_LENGTH = 2;

/**
 * One preview of the home: its heading, the size of the whole list, a link to it, and the
 * first entries as cards, each a link to its page in the home's context.
 */
@Component({
  selector: 'lodb-preview-section',
  imports: [EntityCard, Frame, Image, RouterLink, SeeAllArrow, TranslocoPipe],
  templateUrl: './preview-section.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PreviewSection {
  readonly section = input.required<HomeSection>();
  readonly look = input<CardLook>('tile');

  protected readonly texts = computed(() => RESOURCE_TEXTS[this.section().resource]);
  protected readonly portraitSize = PORTRAIT_SIZE;
  private readonly router = inject(Router);

  protected initialsOf(name: string): string {
    return name.slice(0, INITIALS_LENGTH).toUpperCase();
  }

  /**
   * A card's page as a UrlTree, the shape lodb-entity-card links with: a string would reach
   * the router with its `?lang=` escaped.
   */
  protected hrefOf(link: HomeLink): UrlTree {
    return this.router.createUrlTree([link.path], { queryParams: link.query });
  }
}
