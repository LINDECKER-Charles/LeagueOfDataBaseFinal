import type { APIRequestContext, Page } from "@playwright/test";
import { expect, test } from "../../support/test";

interface Meta {
  readonly latest: string | null;
  readonly versions: readonly string[];
}

const RESOURCES = ["champions", "items", "runes", "summoners"];

async function olderVersion(request: APIRequestContext): Promise<string> {
  const meta = (await (await request.get("/api/meta")).json()) as Meta;
  expect(meta.latest, "the stack must have ingested a version").not.toBeNull();
  return meta.versions[meta.versions.indexOf(meta.latest ?? "") + 1] ?? "";
}

// The four portals of the hero, in the order of the header's codex.
function portals(page: Page) {
  return page
    .getByRole("main")
    .locator("section")
    .first()
    .getByRole("listitem")
    .getByRole("link");
}

test.describe("home", { tag: '@readonly' }, () => {
  test("renders its hero, portals and previews on the server", async ({
    request,
  }) => {
    const response = await request.get("/en/");
    const html = await response.text();

    expect(response.status()).toBe(200);
    expect(html).toContain('ng-server-context="ssr"');
    for (const resource of RESOURCES) {
      expect(html).toContain(`href="/en/${resource}"`);
    }
    expect(html).toContain('href="/en/champions/');
  });

  test("opens four portals and previews each resource, without console error", async ({
    page,
    consoleErrors,
  }) => {
    await page.goto("/en/");

    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    await expect(portals(page)).toHaveCount(RESOURCES.length);
    // The footer has headings of its own: only the page's are counted.
    await expect(
      page.getByRole("main").getByRole("heading", { level: 2 }),
    ).toHaveCount(RESOURCES.length);
    await page.waitForLoadState("networkidle");
    expect(consoleErrors).toEqual([]);
  });

  test("leads through a portal to its list, through the router", async ({
    page,
  }) => {
    await page.goto("/en/");
    await page.waitForLoadState("networkidle");

    await portals(page).nth(1).click();

    await expect.poll(() => new URL(page.url()).pathname).toBe("/en/items");
  });

  test("reads ?version= and carries it into its links, canonical without it", async ({
    page,
    request,
  }) => {
    const older = await olderVersion(request);

    await page.goto(`/en/?version=${older}`);

    await expect(portals(page).first()).toHaveAttribute(
      "href",
      `/en/${older}/champions`,
    );
    await expect(page.locator('link[rel="canonical"]')).toHaveAttribute(
      "href",
      /^https?:\/\/[^/]+\/en\/$/,
    );
  });
});
