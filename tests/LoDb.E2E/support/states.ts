import { expect, type APIRequestContext } from '@playwright/test';
import type { CatalogMeta } from './catalog';

// The pages of an older patch, chosen by the state the API gives their entity there
// (docs/produit/seo-une-page-par-etat.md): Data Dragon's data moves with each patch, so a spec
// asks which entity is in which state rather than naming one.

/** The language of the API answers behind /en/. */
const LANGUAGE = 'en_US';
// Bounds of the search, newest patch first: a few patches back hold every kind of state on a
// stack that indexed a handful of them, and the whole history in production.
const PATCHES_SEARCHED = 12;
const ENTRIES_PER_PATCH = 40;

/** A page of an entity as the API links it: its version, null for the short URL. */
export interface PageLink {
  readonly version: string | null;
  readonly path: string;
}

/** The state of an entity at a version, as its detail carries it. */
export interface EntityState {
  readonly openerVersion: string;
  /** Null while the state is open. */
  readonly lastVersion: string | null;
  readonly isOpen: boolean;
  /** Whether the version of the page opened the state. */
  readonly isOpener: boolean;
  readonly kind: 'new' | 'changed' | 'returned' | 'baseline';
}

/** A page of an entity at an older patch, with the state and the canonical the API gives it. */
export interface StatePage {
  /** The type of the entity, the first segment of its path. */
  readonly type: string;
  readonly id: string;
  readonly version: string;
  /** Its path at that version, below `/{locale}/{version}/`. */
  readonly path: string;
  readonly state: EntityState;
  readonly canonical: PageLink;
}

interface Detail {
  readonly canonicalPath: string;
  readonly canonical: PageLink;
  readonly state: EntityState | null;
}

interface PatchChanges {
  readonly groups: readonly {
    readonly type: string;
    readonly entries: readonly { readonly id: string; readonly kind: string }[];
  }[];
}

async function read<T>(request: APIRequestContext, path: string): Promise<T> {
  const response = await request.get(path);
  expect(response.status(), path).toBe(200);
  return (await response.json()) as T;
}

/** The older patches that have a page of changes, newest first. */
export function olderPatchesWithChanges(meta: CatalogMeta): readonly string[] {
  const { latest, versions, oldestChangesVersion } = meta;
  if (oldestChangesVersion === null) {
    return [];
  }
  const newer = versions.indexOf(latest) + 1;
  return versions.slice(newer, versions.indexOf(oldestChangesVersion) + 1);
}

/** The page of an entity at a version, as the API describes it; null outside the index. */
export async function statePageOf(
  request: APIRequestContext,
  version: string,
  entity: Pick<StatePage, 'type' | 'id'>,
): Promise<StatePage | null> {
  const { type, id } = entity;
  const detail = await read<Detail>(request, `/api/catalog/${version}/${LANGUAGE}/${type}/${id}`);
  if (detail.state === null) {
    return null;
  }
  const { canonicalPath: path, state, canonical } = detail;
  return { type, id, version, path, state, canonical };
}

// The entities a patch opened a state for: changed, new or back, not those it retired.
async function openedAt(request: APIRequestContext, version: string) {
  const changes = await read<PatchChanges>(request, `/api/catalog/${version}/${LANGUAGE}/changes`);
  return changes.groups
    .flatMap(({ type, entries }) => entries.map((entry) => ({ type, ...entry })))
    .filter((entry) => entry.kind !== 'retired')
    .slice(0, ENTRIES_PER_PATCH);
}

/**
 * The first page, from the newest older patch back, of an entity whose state opened at that
 * patch and that `isWanted` accepts; fails when the stack's index holds none (`what` says what
 * was looked for).
 */
export async function findOpenerPage(
  request: APIRequestContext,
  meta: CatalogMeta,
  what: string,
  isWanted: (state: EntityState) => boolean,
): Promise<StatePage> {
  for (const version of olderPatchesWithChanges(meta).slice(0, PATCHES_SEARCHED)) {
    for (const entity of await openedAt(request, version)) {
      const page = await statePageOf(request, version, entity);
      if (page !== null && isWanted(page.state)) {
        return page;
      }
    }
  }
  throw new Error(
    `The stack's index of states holds no ${what}: index the patches before the latest first` +
      ' (tools/states/index-past-patches.sh)',
  );
}
