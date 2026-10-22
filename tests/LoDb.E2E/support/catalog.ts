import { expect, type APIRequestContext, type Page, type Response } from '@playwright/test';

interface RawMeta {
  readonly latest: string | null;
  readonly versions: readonly string[];
  readonly oldestChangesVersion: string | null;
}

interface DetailNeighbour {
  readonly canonicalPath: string;
  readonly edition: 'modern' | 'classic';
}

// The time a detail page's entity took, which its server sends for the load-time badge.
const SERVER_TIMING = /(?:^|,\s*)catalogue;dur=\d+(?:\.\d+)?/;

/** The entries on either side of a detail page, as its payload carries them. */
export interface DetailNeighbours {
  readonly previous: DetailNeighbour | null;
  readonly next: DetailNeighbour | null;
}

/** The versions of the stack, newest first; `latest` is the one short URLs show. */
export interface CatalogMeta {
  readonly latest: string;
  readonly versions: readonly string[];
  /**
   * The oldest version with a page of changes: every one from it up to the latest has one.
   * Null while the index of states covers fewer than two versions.
   */
  readonly oldestChangesVersion: string | null;
}

/** The versions of the stack; fails when it has not ingested any. */
export async function metaOf(request: APIRequestContext): Promise<CatalogMeta> {
  const meta = (await (await request.get('/api/meta')).json()) as RawMeta;
  expect(meta.latest, 'the stack must have ingested a version').not.toBeNull();
  return {
    latest: meta.latest ?? '',
    versions: meta.versions,
    oldestChangesVersion: meta.oldestChangesVersion,
  };
}

/** The version `steps` patches before the latest, pinned in the path of an archived page. */
export function olderVersion(meta: CatalogMeta, steps = 1): string {
  const version = meta.versions[meta.versions.indexOf(meta.latest) + steps];
  expect(version, `the stack must know ${steps} version(s) before ${meta.latest}`).toBeDefined();
  return version ?? '';
}

/**
 * A detail page of /en/ as its server sends it, before any script: the time its entity took,
 * and a pager linking the payload's neighbours, a LoL Classic one chipped with its edition.
 */
export async function expectServedDetail(
  page: Page,
  response: Response | null,
  neighbours: DetailNeighbours,
): Promise<void> {
  expect(response?.headers()['server-timing']).toMatch(SERVER_TIMING);
  const sides = [
    ['prev', neighbours.previous],
    ['next', neighbours.next],
  ] as const;
  for (const [rel, neighbour] of sides) {
    const link = page.locator(`lodb-pager a[rel="${rel}"]`);
    await expect(link).toHaveCount(neighbour === null ? 0 : 1);
    if (neighbour !== null) {
      await expect(link).toHaveAttribute('href', `/en/${neighbour.canonicalPath}`);
      const chips = neighbour.edition === 'classic' ? 1 : 0;
      await expect(link.locator('.hx-chip-hex')).toHaveCount(chips);
    }
  }
}
