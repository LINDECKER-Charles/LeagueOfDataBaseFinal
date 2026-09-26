import { FAVORITE_SLOT } from '../../../core/api/generated/models/favorite-slot-array';
import type { PublicProfile } from '../../../core/api/generated/models/public-profile';
import { imageSource } from '../shared/image-source';
import { initialsOf } from '../shared/initials-of';
import type { HeroOrb } from './hero-orb';

/**
 * The favorites the hero of a public card floats, in the order of the slots: those its
 * patch resolves, but the champion when its splash is already the backdrop.
 */
export function heroOrbsOf(profile: PublicProfile): HeroOrb[] {
  const championIsBackdrop = profile.backdrop?.kind === 'champion';
  return FAVORITE_SLOT.filter((slot) => !(slot === 'champion' && championIsBackdrop))
    .flatMap((slot) => {
      const current = profile.showcase.favorites[slot].current;
      return current === null ? [] : [{ slot, current }];
    })
    .map(({ slot, current }, index) => ({
      slot,
      image: imageSource(current.image),
      initials: initialsOf(current.name),
      primary: slot === 'champion',
      position: index + 1,
    }));
}
