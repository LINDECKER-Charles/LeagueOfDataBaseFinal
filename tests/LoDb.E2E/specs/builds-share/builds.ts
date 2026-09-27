import { type APIRequestContext, expect, type Page } from '@playwright/test';
import { metaOf } from '../../support/catalog';
import { register, type TestAccount } from '../account/accounts';
import { accountLinkIn, lastMailTo } from '../account/mailbox';

/** What the suite asks of a build it creates; the rest is taken from the pickers. */
export interface BuildOptions {
  readonly name: string;
  readonly isPublic: boolean;
  /** A Data Dragon language, which the shared page then speaks. */
  readonly language: string;
  readonly gameMode: 'sr' | 'aram' | 'arena' | 'nexus_blitz';
}

/** A build the suite created, as the API answered it. */
export interface CreatedBuild {
  readonly id: number;
  readonly shareToken: string;
  readonly name: string;
  readonly gameVersion: string;
  readonly championId: string;
}

interface Option {
  readonly id: string;
  readonly name: string;
  readonly gold?: number;
}

interface RuneTree {
  readonly id: number;
  readonly slots: readonly (readonly { readonly id: number }[])[];
}

/** The runes and items the suite builds with, read from the pickers of the latest patch. */
export interface BuildParts {
  readonly champion: Option;
  readonly items: readonly Option[];
  readonly runes: {
    readonly primaryStyleId: number;
    readonly primarySelections: readonly number[];
    readonly secondaryStyleId: number;
    readonly secondarySelections: readonly number[];
  };
}

const CREATED = 201;
// The pickers answer for an explicit patch and language only; builds are made on the latest.
const PICKER_LANGUAGE = 'en_US';
// Two items with a price: the purchase order shows them, and its total is not nil.
const ITEMS = 2;

/**
 * Registers the account and confirms its address through the e-mail: the API creates builds
 * for a verified account only. The page is left signed in.
 */
export async function verifiedAccount(
  page: Page,
  request: APIRequestContext,
  account: TestAccount,
): Promise<void> {
  await register(page, account);
  await page.goto(accountLinkIn(await lastMailTo(request, account.email), 'verify-email'));
  await expect(
    page.getByText('Your email address is confirmed. Have fun, summoner.'),
  ).toBeVisible();
}

// The headers of an unsafe request of the page's session: the site's origin and the XSRF
// token the sign-in issued as a cookie.
async function unsafeHeaders(page: Page): Promise<Record<string, string>> {
  const origin = new URL(page.url()).origin;
  const cookies = await page.context().cookies(origin);
  const xsrf = cookies.find((cookie) => cookie.name === 'XSRF-TOKEN')?.value ?? '';
  return { Origin: origin, 'X-XSRF-TOKEN': xsrf };
}

// The first entry of a list the stack must not leave empty.
function firstOf<T>(list: readonly T[] | undefined, what: string): T {
  const [first] = list ?? [];
  if (first === undefined) {
    throw new Error(`the stack offers no ${what}`);
  }
  return first;
}

async function pick<T>(page: Page, path: string): Promise<T> {
  const response = await page.request.get(path);
  expect(response.ok(), `the picker ${path} answers`).toBe(true);
  return (await response.json()) as T;
}

/**
 * A valid page of runes and purchase order for `mode`: the first champion, the first perk of
 * each slot of the first tree, the first of the next two rows of the second tree, and the
 * first items with a price.
 */
export async function buildParts(page: Page, mode: BuildOptions['gameMode']): Promise<BuildParts> {
  const { latest } = await metaOf(page.request);
  const scope = `version=${latest}&lang=${PICKER_LANGUAGE}`;
  const champions = await pick<{ options: Option[] }>(page, `/api/pickers/champions?${scope}`);
  const runes = await pick<{ trees: RuneTree[] }>(page, `/api/pickers/runes?${scope}`);
  const items = await pick<{ options: Option[] }>(page, `/api/pickers/items?${scope}&mode=${mode}`);
  const primary = firstOf(runes.trees, 'rune tree');
  const secondary = firstOf(runes.trees.slice(1), 'second rune tree');
  const perkOf = (slot: RuneTree['slots'][number]) => firstOf(slot, 'perk in a slot').id;
  return {
    champion: firstOf(champions.options, 'champion'),
    items: items.options.filter((item) => (item.gold ?? 0) > 0).slice(0, ITEMS),
    runes: {
      primaryStyleId: primary.id,
      primarySelections: primary.slots.slice(0, 4).map(perkOf),
      secondaryStyleId: secondary.id,
      secondarySelections: secondary.slots.slice(1, 3).map(perkOf),
    },
  };
}

/** Creates a build through the API, as the signed-in owner of the page. */
export async function createBuild(page: Page, options: BuildOptions): Promise<CreatedBuild> {
  const parts = await buildParts(page, options.gameMode);
  const response = await page.request.post('/api/builds', {
    headers: await unsafeHeaders(page),
    data: {
      ...options,
      description: `${options.name}, forged by the end-to-end suite.`,
      structure: {
        championId: parts.champion.id,
        runes: parts.runes,
        steps: [{ label: 'Core', note: null, items: parts.items.map((item) => item.id) }],
      },
    },
  });
  expect(response.status(), `the API creates ${options.name}`).toBe(CREATED);
  const build = (await response.json()) as CreatedBuild & { structure: { championId: string } };
  return { ...build, championId: build.structure.championId };
}
