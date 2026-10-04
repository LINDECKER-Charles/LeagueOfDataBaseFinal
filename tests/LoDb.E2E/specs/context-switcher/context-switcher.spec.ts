import type { APIRequestContext, BrowserContext, Page } from "@playwright/test";
import { hydrated } from "../../support/hydration";
import { expect, test } from "../../support/test";

interface Meta {
  readonly latest: string | null;
  readonly versions: readonly string[];
}

const PREFERENCES_COOKIE = "lod_prefs";
// Every internal navigation sends its page-view beacon: telemetry, not a preference.
const ANALYTICS_PATH = "/api/analytics/";
// A cold patch: its datasets, then the images of a list, fetched from Data Dragon.
const WARM_UP_TIMEOUT_MS = 60_000;

async function olderVersion(request: APIRequestContext): Promise<string> {
  const meta = (await (await request.get("/api/meta")).json()) as Meta;
  expect(meta.latest, "the stack must have ingested a version").not.toBeNull();
  return meta.versions[meta.versions.indexOf(meta.latest ?? "") + 1] ?? "";
}

function switcher(page: Page) {
  return page.locator("lodb-context-switcher");
}

function panelOf(page: Page) {
  return switcher(page).locator("details");
}

// Opens the panel once the app drives the switcher. A completed navigation folds the panel
// (lodbDisclosure): the initial one only ends before the app first settles, which hydrated()
// waits for, and the options the browser loads are there by then.
async function openSwitcher(page: Page): Promise<void> {
  await hydrated(switcher(page));
  await expect(
    switcher(page).locator("#switcher-version option"),
  ).not.toHaveCount(0);
  await expect(panelOf(page)).not.toHaveAttribute("open");
  await switcher(page).locator("summary").click();
  await expect(panelOf(page)).toHaveAttribute("open", "");
}

function loaderOf(page: Page) {
  return page.locator("lodb-loader-dialog");
}

// Sends the choice, then waits for the loader to come and go: another context is warmed
// behind it before the visit, for seconds when the stack has not ingested the patch yet.
async function apply(page: Page): Promise<void> {
  await switcher(page).getByRole("button").click();
  await expect(panelOf(page)).not.toHaveAttribute("open");
  await expect(loaderOf(page)).toBeVisible();
  await expect(loaderOf(page)).toBeHidden({ timeout: WARM_UP_TIMEOUT_MS });
}

async function preferencesCookie(
  context: BrowserContext,
): Promise<string | undefined> {
  const cookies = await context.cookies();
  return cookies.find((cookie) => cookie.name === PREFERENCES_COOKIE)?.value;
}

function pathAndQuery(page: Page): string {
  const url = new URL(page.url());
  return `${url.pathname}${url.search}`;
}

test.describe("context switcher", { tag: '@readonly' }, () => {
  test("ships no option in the prerendered HTML: the browser loads them", async ({
    page,
    request,
  }) => {
    const html = await (await request.get("/en/about")).text();

    expect(html).toContain('id="switcher-version"');
    expect(html).not.toContain("<option");
    await page.goto("/en/about");
    await expect(
      switcher(page).locator("#switcher-version option"),
    ).not.toHaveCount(0);
  });

  test("pins an older patch on the same page, without a POST", async ({
    page,
    request,
    context,
    consoleErrors,
  }) => {
    const older = await olderVersion(request);
    const posts: string[] = [];
    page.on("request", (sent) => {
      if (
        sent.method() === "POST" &&
        !new URL(sent.url()).pathname.startsWith(ANALYTICS_PATH)
      ) {
        posts.push(sent.url());
      }
    });
    await page.goto("/en/champions");

    await openSwitcher(page);
    await switcher(page).locator("#switcher-version").selectOption(older);
    await apply(page);

    await expect.poll(() => pathAndQuery(page)).toBe(`/en/${older}/champions`);
    expect(posts).toEqual([]);
    expect(await preferencesCookie(context)).toBeUndefined();
    expect(consoleErrors).toEqual([]);
  });

  // The legacy loader: the patch is warmed behind a modal naming the page's lists, then
  // visited. It holds its full bar a beat even on a warm patch, which the check relies on.
  test("warms the chosen patch behind the loader, then lands on the page", async ({
    page,
    request,
    consoleErrors,
  }) => {
    const older = await olderVersion(request);
    await page.goto("/en/champions");

    await openSwitcher(page);
    await switcher(page).locator("#switcher-version").selectOption(older);
    await switcher(page).getByRole("button").click();

    const loader = page.getByRole("dialog", { name: "Summoning data" });
    await expect(loader).toBeVisible();
    await expect(loader.getByRole("listitem")).toHaveText([/Champions/]);
    await expect(loader.getByRole("progressbar")).toBeAttached();
    await expect(loader).toBeHidden({ timeout: WARM_UP_TIMEOUT_MS });
    expect(pathAndQuery(page)).toBe(`/en/${older}/champions`);
    expect(consoleErrors).toEqual([]);
  });

  // The legacy printed the session's patch in the chip of every page (page_selection).
  test("names the patch of the session on the pages that read none", async ({
    page,
    request,
  }) => {
    const older = await olderVersion(request);
    await page.goto("/en/about");

    await openSwitcher(page);
    await switcher(page).locator("#switcher-version").selectOption(older);
    await apply(page);
    await page.locator('header nav a[href="/en/trends"]').click();
    await expect.poll(() => pathAndQuery(page)).toBe("/en/trends");

    await expect(switcher(page).locator("summary")).toContainText(older);
    await expect(switcher(page).locator("#switcher-version")).toHaveValue(older);
    await expect(
      page.locator(".bottom-nav a").nth(1),
    ).toHaveAttribute("href", `/en/${older}/champions`);
  });

  test("moves to another locale, and to a regional variant through ?lang=", async ({
    page,
  }) => {
    await page.goto("/en/items");

    await openSwitcher(page);
    await switcher(page).locator("#switcher-language").selectOption("fr:fr_FR");
    await apply(page);
    await expect.poll(() => pathAndQuery(page)).toBe("/fr/items");

    await openSwitcher(page);
    await switcher(page).locator("#switcher-language").selectOption("en:en_GB");
    await apply(page);
    await expect.poll(() => pathAndQuery(page)).toBe("/en/items?lang=en_GB");
  });

  test("remembers the choice when asked, and applies it to the next page", async ({
    page,
    request,
    context,
  }) => {
    const older = await olderVersion(request);
    await page.goto("/en/runes");

    await openSwitcher(page);
    const remember = switcher(page).locator("#switcher-remember");
    await expect(remember).toBeVisible();
    await remember.check();
    await switcher(page).locator("#switcher-version").selectOption(older);
    await expect(switcher(page).locator("#switcher-version")).toHaveValue(older);
    await apply(page);
    await expect.poll(() => pathAndQuery(page)).toBe(`/en/${older}/runes`);

    expect(await preferencesCookie(context)).toBe(`loc=en&v=${older}`);
    await page.goto("/en/summoners");
    await expect.poll(() => pathAndQuery(page)).toBe(`/en/${older}/summoners`);
  });
});
