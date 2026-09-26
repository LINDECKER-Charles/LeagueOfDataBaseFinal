import { expectNoAccessibilityViolations } from "../../support/accessibility";
import { expect, test } from "../../support/test";

test.describe("legal pages", () => {
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
    const text = page.locator(".hx-prose").locator("..");

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
    const link = page.locator(".section-nav a").nth(2);
    const target =
      ((await link.getAttribute("href")) ?? "").split("#")[1] ?? "";

    await link.click();

    await expect(page).toHaveURL(new RegExp(`/en/legal/cookies#${target}$`));
    await expect(page.locator(`section#${target}`)).toBeInViewport();
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
