import { expect, type APIRequestContext } from '@playwright/test';

interface RawMeta {
  readonly latest: string | null;
  readonly versions: readonly string[];
}

interface List {
  readonly entries: readonly { readonly id: string }[];
}

/** The versions of the stack, newest first; `latest` is the one short URLs show. */
export interface CatalogMeta {
  readonly latest: string;
  readonly versions: readonly string[];
}

/** The versions of the stack; fails when it has not ingested any. */
export async function metaOf(request: APIRequestContext): Promise<CatalogMeta> {
  const meta = (await (await request.get('/api/meta')).json()) as RawMeta;
  expect(meta.latest, 'the stack must have ingested a version').not.toBeNull();
  return { latest: meta.latest ?? '', versions: meta.versions };
}

/** The version `steps` patches before the latest, pinned in the path of an archived page. */
export function olderVersion(meta: CatalogMeta, steps = 1): string {
  const version = meta.versions[meta.versions.indexOf(meta.latest) + steps];
  expect(version, `the stack must know ${steps} version(s) before ${meta.latest}`).toBeDefined();
  return version ?? '';
}

/** The ids of a resource (`champions`, `items`…) in a version, in en_US. */
export async function idsOf(
  request: APIRequestContext,
  version: string,
  resource: string,
): Promise<string[]> {
  const response = await request.get(`/api/catalog/${version}/en_US/${resource}`);
  expect(response.status(), `${resource} of ${version}`).toBe(200);
  return ((await response.json()) as List).entries.map((entry) => entry.id);
}
