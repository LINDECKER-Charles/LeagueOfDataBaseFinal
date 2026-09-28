import type { ChampionSkin } from '../../../../../core/api/generated/models/champion-skin';

/** What the skin viewer opens on: the gallery's skins, and the one clicked. */
export interface SkinViewerData {
  readonly championName: string;
  readonly skins: readonly ChampionSkin[];
  readonly index: number;
}
