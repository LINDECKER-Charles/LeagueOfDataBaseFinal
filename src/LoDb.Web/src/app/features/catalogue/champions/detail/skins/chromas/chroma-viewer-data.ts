import type { ChampionChroma } from '../../../../../../core/api/generated/models/champion-chroma';

/** What the chroma viewer opens on: a skin's chromas, and the one clicked. */
export interface ChromaViewerData {
  readonly skinName: string;
  readonly chromas: readonly ChampionChroma[];
  readonly index: number;
}
