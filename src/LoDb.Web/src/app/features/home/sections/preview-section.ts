import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink, type UrlTree } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { EntityCard } from '../../../ui/cards/entity-card';
import { Image } from '../../../ui/media/image';
import { EmptyState } from '../../../ui/surfaces/empty-state';
import { Frame } from '../../../ui/surfaces/frame';
import type { HomeLink } from '../data/home-link';
import type { HomeSection } from '../data/home-section';
import type { CardLook } from './card-look';
import { RESOURCE_TEXTS } from './resource-texts';
import { SeeAllArrow } from './see-all-arrow';

// Pixel boxes reserved before the bytes arrive: Data Dragon's loading-screen portraits are
// 308 by 560.
const LOADING_ART_WIDTH = 308;
const LOADING_ART_HEIGHT = 560;
const INITIALS_LENGTH = 2;
// Spelled out whole for the Tailwind scanner: portraits go three abreast from 640px, as the
// legacy home's champions did, tiles two.
const GRID_CLASSES: Readonly<Record<CardLook, string>> = {
  portrait: 'grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4',
  tile: 'grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4',
  round: 'grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4',
};

/**
 * One preview of the home: its heading, the size of the whole list, a link to it, and the
 * first entries as cards, each a link to its page in the home's context. An empty preview
 * of champions or rune paths says so in the framed empty state, as the legacy home did.
 */
@Component({
  selector: 'lodb-preview-section',
  imports: [EmptyState, EntityCard, Frame, Image, RouterLink, SeeAllArrow, TranslocoPipe],
  templateUrl: './preview-section.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PreviewSection {
  readonly section = input.required<HomeSection>();
  readonly look = input<CardLook>('tile');

  protected readonly texts = computed(() => RESOURCE_TEXTS[this.section().resource]);
  protected readonly gridClass = computed(() => GRID_CLASSES[this.look()]);
  protected readonly loadingArtWidth = LOADING_ART_WIDTH;
  protected readonly loadingArtHeight = LOADING_ART_HEIGHT;
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
