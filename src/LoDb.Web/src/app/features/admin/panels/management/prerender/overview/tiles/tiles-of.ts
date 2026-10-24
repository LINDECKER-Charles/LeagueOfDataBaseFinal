import type { PrerenderSpeedReport } from '../../../../../../../core/api/generated/models/prerender-speed-report';
import type { AdminText } from '../../../../../shared/inject-admin-text';
import type { StatusTile } from '../../../../../widgets/status-tabs/status-tile';
import { PRERENDER_VIEWS, type PrerenderView } from '../view/prerender-view';
import { viewKey } from '../view/view-key';
import { coverageTileOf } from './coverage-tile-of';
import { historyTileOf } from './history-tile-of';
import { type PassTileSource, passTileOf } from './pass-tile-of';
import { type QueuesTileSource, queuesTileOf } from './queues-tile-of';
import { speedTileOf } from './speed-tile-of';
import { storageTileOf } from './storage-tile-of';

/** What the tiles of the page read: its report, and the reads beside it. */
export interface TileSources extends PassTileSource, QueuesTileSource {
  readonly speed: PrerenderSpeedReport | undefined;
}

/**
 * The tiles of the page's views (`StatusTabs`), in the order of `PRERENDER_VIEWS`, each keyed
 * as the panel of its view: the page's key figures and its tabs at once.
 */
export function tilesOf(sources: TileSources, texts: AdminText): StatusTile[] {
  const faces: Readonly<Record<PrerenderView, Omit<StatusTile, 'key'>>> = {
    pass: passTileOf(sources, texts),
    coverage: coverageTileOf(sources.report, texts),
    queues: queuesTileOf(sources, texts),
    speed: speedTileOf(sources.speed, texts),
    storage: storageTileOf(sources.report, texts),
    history: historyTileOf(sources.report, texts),
  };
  return PRERENDER_VIEWS.map((view) => ({ key: viewKey.of(view), ...faces[view] }));
}
