import { expect, test } from "../../support/test";

const PAGES = [
  "about",
  "about/data",
  "faq",
  "changelog",
  "legal/notice",
  "legal/privacy",
  "legal/terms",
  "legal/cookies",
];
const LOCALES = ["en", "fr", "ar", "zh-hant"];

// What the server answers before any script runs: the file the build prerendered.
test.describe("prerendered editorial pages", () => {
  test.use({ javaScriptEnabled: false });

  for (const locale of LOCALES) {
    for (const path of PAGES) {
      test(`serves /${locale}/${path} as a static page`, async ({ page }) => {
        const response = await page.goto(`/${locale}/${path}`);

        expect(response?.status()).toBe(200);
        await expect(page.locator("lodb-root")).toHaveAttribute(
          "ng-server-context",
          "ssg",
        );
        await expect(page.locator("html")).toHaveAttribute("lang", locale);
        await expect(page.locator("h1")).toHaveCount(1);
        await expect(page.locator("h1")).not.toHaveText(
          /^\s*[a-z_]+(\.[a-z_]+)+\s*$/,
        );
      });
    }
  }
});
