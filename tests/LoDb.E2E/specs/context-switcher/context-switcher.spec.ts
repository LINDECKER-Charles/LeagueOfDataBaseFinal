import type { APIRequestContext, BrowserContext, Page } from "@playwright/test";
import { expect, test } from "../../support/test";

interface Meta {
  readonly latest: string | null;
  readonly versions: readonly string[];
}

const PREFERENCES_COOKIE = "lod_prefs";
// Every internal navigation sends its page-view beacon: telemetry, not a preference.
const ANALYTICS_PATH = "/api/analytics/";

async function olderVersion(request: APIRequestContext): Promise<string> {
  const meta = (await (await request.get("/api/meta")).json()) as Meta;
  expect(meta.latest, "the stack must have ingested a version").not.toBeNull();
  return meta.versions[meta.versions.indexOf(meta.latest ?? "") + 1] ?? "";
}

function switcher(page: Page) {
  return page.locator("lodb-context-switcher");
}

// A completed navigation folds the panel (lodbDisclosure): the first one may end after the
// options have loaded, so the panel is reopened until it stays open.
async function ensureOpen(page: Page): Promise<void> {
  const panel = switcher(page).locator("details");
  await expect(async () => {
    if ((await panel.getAttribute("open")) === null) {
      await switcher(page).locator("summary").click();
    }
    await expect(panel).toHaveAttribute("open", "", { timeout: 1_000 });
  }).toPass();
}

// Opens the panel once the browser has loaded the options.
async function openSwitcher(page: Page): Promise<void> {
  await expect(
    switcher(page).locator("#switcher-version option"),
  ).not.toHaveCount(0);
  await ensureOpen(page);
}

async function apply(page: Page): Promise<void> {
  await switcher(page).getByRole("button").click();
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

test.describe("context switcher", () => {
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

    expect(await preferencesCookie(context)).toBe(`v=${older}`);
    await page.goto("/en/summoners");
    await expect.poll(() => pathAndQuery(page)).toBe(`/en/${older}/summoners`);
  });
});
