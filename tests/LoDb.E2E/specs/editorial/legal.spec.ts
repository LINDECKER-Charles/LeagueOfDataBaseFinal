import type { Locator } from "@playwright/test";
import { expectNoAccessibilityViolations } from "../../support/accessibility";
import { expect, test } from "../../support/test";

// The scroll margin of a prose section (6.5rem), a pixel short for rounding.
const SCROLL_MARGIN = 103;

async function topOf(section: Locator): Promise<number> {
  return section.evaluate((element) => element.getBoundingClientRect().top);
}

test.describe("legal pages", { tag: '@readonly' }, () => {
  test("show the French text under fr", async ({ page, consoleErrors }) => {
    await page.goto("/fr/legal/notice");
    await page.waitForLoadState("networkidle");

    await expect(page.locator("section#editeur h2")).toContainText(
      "Éditeur du site",
    );
    await expect(page.locator("section#publisher")).toHaveCount(0);
    expect(consoleErrors).toEqual([]);
  });

  test("show the English text under another locale, marked as English", async ({
    page,
  }) => {
    await page.goto("/de/legal/privacy");
    const text = page.locator(".hx-prose");

    await expect(text).toHaveAttribute("lang", "en");
    await expect(page.locator("html")).toHaveAttribute("lang", "de");
    await expect(page.locator(".hx-prose section").first()).toHaveAttribute(
      "id",
      /^[a-z-]+$/,
    );
  });

  test("scroll to a section from the table of contents", async ({ page }) => {
    await page.goto("/en/legal/cookies");
    await page.waitForLoadState("networkidle");
    const contents = page.getByRole("navigation", { name: "Contents" });
    const link = contents.getByRole("link").nth(2);
    const target =
      ((await link.getAttribute("href")) ?? "").split("#")[1] ?? "";

    await link.click();

    await expect(page).toHaveURL(new RegExp(`/en/legal/cookies#${target}$`));
    const section = page.locator(`section#${target}`);
    await expect(section).toBeInViewport();
    // Below its scroll margin (6.5rem), clear of the sticky header, as a native jump lands.
    expect(await topOf(section)).toBeGreaterThanOrEqual(SCROLL_MARGIN);
    // As after a native jump: the next Tab starts in the section, and Back returns to the top.
    await expect(section).toBeFocused();
    await page.goBack();
    await expect(page).toHaveURL(/\/en\/legal\/cookies$/);
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
  });

  test("jump to a section from a link in the text", async ({ page }) => {
    await page.goto("/en/legal/privacy");
    await page.waitForLoadState("networkidle");

    await page.locator('a[href="/en/legal/privacy#audience"]').first().click();

    await expect(page).toHaveURL(/\/en\/legal\/privacy#audience$/);
    expect(await topOf(page.locator("section#audience"))).toBeGreaterThanOrEqual(
      SCROLL_MARGIN,
    );
  });

  test("link the other legal pages within the locale", async ({ page }) => {
    await page.goto("/es/legal/notice");

    await page.locator('section#data a[href="/es/legal/privacy"]').click();

    await expect(page).toHaveURL(/\/es\/legal\/privacy$/);
    await expect(page.locator("html")).toHaveAttribute("lang", "es");
  });

  test("have no accessibility violation", async ({ page }) => {
    await page.goto("/fr/legal/terms");
    await page.waitForLoadState("networkidle");

    await expectNoAccessibilityViolations(page);
  });
});
