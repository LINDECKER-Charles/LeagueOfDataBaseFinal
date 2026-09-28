import assert from 'node:assert/strict';
import path from 'node:path';
import { describe, test } from 'node:test';
import { ESLint } from 'eslint';

// The rules are exercised through the real workspace configuration, so a broken glob or a
// rule dropped from eslint.config.mjs fails here, not only a broken rule implementation.
const WORKSPACE = path.resolve(import.meta.dirname, '..');
const eslint = new ESLint({ cwd: WORKSPACE });

async function reportedRules(relativeFile, code) {
  const [result] = await eslint.lintText(code, { filePath: path.join(WORKSPACE, relativeFile) });
  // A parse error reports no rule at all and would make every "accepts" case pass silently.
  assert.equal(result.fatalErrorCount, 0, JSON.stringify(result.messages));
  return result.messages.map((message) => message.ruleId);
}

describe('lodb/native-plugin-imports', () => {
  const rule = 'lodb/native-plugin-imports';

  test('refuses a Capacitor import in a feature', async () => {
    const code = "import { Capacitor } from '@capacitor/core';\nexport const c = Capacitor;\n";
    assert.ok((await reportedRules('src/app/features/home/home-page.ts', code)).includes(rule));
  });

  test('refuses a lazy Capawesome import in core outside platform', async () => {
    const code = "export const load = () => import('@capawesome/capacitor-app-update');\n";
    assert.ok((await reportedRules('src/app/core/http/updates.ts', code)).includes(rule));
  });

  test('refuses a re-export outside src/app', async () => {
    const code = "export * from '@capacitor/app';\n";
    assert.ok((await reportedRules('src/main.ts', code)).includes(rule));
  });

  test('accepts native plugins inside core/platform', async () => {
    const code = "import { App } from '@capacitor/app';\nexport const app = App;\n";
    const file = 'src/app/core/platform/android/android-platform.ts';
    assert.ok(!(await reportedRules(file, code)).includes(rule));
  });

  test('ignores look-alike packages', async () => {
    const code = "import { x } from '@capacitor-community/sqlite';\nexport const y = x;\n";
    assert.ok(!(await reportedRules('src/app/ui/button/button.ts', code)).includes(rule));
  });
});

describe('lodb/feature-boundaries', () => {
  const rule = 'lodb/feature-boundaries';
  const editor = 'src/app/features/builds/editor/editor-page.ts';
  const importing = (source) => `import { x } from '${source}';\nexport const y = x;\n`;

  test('accepts core, ui, packages and the feature itself', async () => {
    for (const source of [
      '../../../core/i18n/locales',
      '../../../ui/button/button',
      '../shared/build-card',
      './editor-store',
      '..',
      '@angular/core',
    ]) {
      assert.ok(!(await reportedRules(editor, importing(source))).includes(rule), source);
    }
  });

  test('refuses another feature, the app root and files outside src/app', async () => {
    for (const source of [
      '../../catalogue/champions/champion-page',
      '../../../app.routes',
      '../../../../environments/environment',
    ]) {
      assert.ok((await reportedRules(editor, importing(source))).includes(rule), source);
    }
  });

  test('refuses re-exports and lazy imports of another feature', async () => {
    const reexport = "export * from '../../home/home-page';\n";
    const lazy = "export const load = () => import('../../home/home-page');\n";
    assert.ok((await reportedRules(editor, reexport)).includes(rule));
    assert.ok((await reportedRules(editor, lazy)).includes(rule));
  });

  test('leaves files outside features alone', async () => {
    const code = importing('./features/home/home-page');
    assert.ok(!(await reportedRules('src/app/app.routes.ts', code)).includes(rule));
  });
});
