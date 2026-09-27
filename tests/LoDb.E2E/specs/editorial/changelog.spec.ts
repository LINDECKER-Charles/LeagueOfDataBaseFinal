import { expectNoAccessibilityViolations } from "../../support/accessibility";
import { expect, test } from "../../support/test";

const VERSION = /^v\d+\.\d+\.\d+$/;

test.describe("changelog", { tag: '@readonly' }, () => {
  test("lists the releases newest first, the newest one open", async ({
    page,
    consoleErrors,
  }) => {
    await page.goto("/en/changelog");
    await page.waitForLoadState("networkidle");
    const releases = page.locator(".timeline > li");

    expect(await releases.count()).toBeGreaterThan(1);
    await expect(releases.first().locator("details")).toHaveAttribute(
      "open",
      "",
    );
    await expect(releases.nth(1).locator("details")).not.toHaveAttribute(
      "open",
    );
    await expect(releases.first()).toHaveAttribute("id", /^v\d+-\d+-\d+$/);
    expect(consoleErrors).toEqual([]);
  });

  test("shows the release version in the header chip and on the page", async ({
    page,
  }) => {
    await page.goto("/fr/changelog");

    await expect(page.locator(".hx-version-chip--lg")).toHaveText(VERSION);
    const version =
      (await page.locator(".hx-version-chip--lg").textContent())?.trim() ?? "";
    await expect(page.locator(".timeline > li").first()).toHaveAttribute(
      "id",
      version.replace(/\./g, "-"),
    );
  });

  test("marks the French notes of a release read under another locale", async ({
    page,
  }) => {
    await page.goto("/de/changelog");

    await expect(
      page.locator(".timeline > li").first().locator(".release__body"),
    ).toHaveAttribute("lang", "fr");
  });

  test("opens a closed release on its summary", async ({ page }) => {
    await page.goto("/en/changelog");
    const second = page.locator(".timeline > li").nth(1).locator("details");

    await second.locator("summary").click();

    await expect(second).toHaveAttribute("open", "");
  });

  test("has no accessibility violation", async ({ page }) => {
    await page.goto("/en/changelog");
    await page.waitForLoadState("networkidle");

    await expectNoAccessibilityViolations(page);
  });
});
