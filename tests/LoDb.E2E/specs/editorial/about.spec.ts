import { expectNoAccessibilityViolations } from "../../support/accessibility";
import { expect, test } from "../../support/test";

const NUMBER = /^\s*\d[\d\s., ]*\s*$/;

test.describe("about pages", { tag: '@readonly' }, () => {
  test("prerender the inventory as placeholders", async ({ browser }) => {
    const context = await browser.newContext({ javaScriptEnabled: false });
    const page = await context.newPage();

    await page.goto("/en/about");

    await expect(page.locator("lodb-inventory-facts dd").first()).toHaveText(
      "—",
    );
    await context.close();
  });

  test("fill the inventory counters in the browser", async ({
    page,
    consoleErrors,
  }) => {
    await page.goto("/en/about");

    await expect(page.locator("lodb-inventory-counters dd").first()).toHaveText(
      NUMBER,
    );
    expect(consoleErrors).toEqual([]);
  });

  test("fill the data snapshot with the latest version", async ({ page }) => {
    await page.goto("/fr/about/data");

    await expect(page.locator("lodb-inventory-counters dd").first()).toHaveText(
      /\d+\.\d+/,
    );
  });

  test("describe the data set to crawlers", async ({ page }) => {
    await page.goto("/en/about/data");
    const graphs = await page
      .locator('script[type="application/ld+json"]')
      .allTextContents();

    expect(graphs.some((graph) => graph.includes('"@type":"Dataset"'))).toBe(
      true,
    );
  });

  test("answer the FAQ with an anchor per question", async ({ page }) => {
    await page.goto("/en/faq");
    const graphs = await page
      .locator('script[type="application/ld+json"]')
      .allTextContents();

    expect(graphs.some((graph) => graph.includes('"@type":"FAQPage"'))).toBe(
      true,
    );
    await expect(
      page.locator("#questions dl > div#what-is-it dt"),
    ).toBeVisible();
  });

  test("have no accessibility violation", async ({ page }) => {
    await page.goto("/en/about");
    await page.waitForLoadState("networkidle");

    await expectNoAccessibilityViolations(page);
  });
});
