import type { ChampionSkin } from '../../../../../core/api/generated/models/champion-skin';

/** The skins a gallery shows: all but the base one, which the hero already wears. */
export function alternateSkins(skins: readonly ChampionSkin[]): ChampionSkin[] {
  return skins.filter((skin) => skin.number !== 0);
}
