// `npm run i18n:convert` (src/LoDb.Web): one-off conversion of the Symfony YAML catalogues
// (app/translations) into the Transloco JSON catalogues (src/LoDb.Web/public/i18n).
// Once converted, the JSON files are the source: re-running overwrites them.
// Tests of the conversion and of the report: `node --test "tools/next/i18n/**/*.test.mjs"`.
import { relative } from 'node:path';
import { I18N_PATHS } from './lib/i18n-paths.mjs';
import { runConversion } from './lib/convert/run-conversion.mjs';

const written = runConversion({
  yamlDir: I18N_PATHS.yamlDir,
  catalogueDir: I18N_PATHS.catalogueDir,
});
const total = (field) => written.reduce((sum, file) => sum + file[field], 0);

for (const file of written) {
  console.log(`${relative(I18N_PATHS.repoRoot, file.file)}: ${file.messages} messages`);
}
console.log(
  `i18n:convert: ${written.length} catalogues, ${total('messages')} messages, ` +
    `${total('pluralPairs')} X/X_one pairs and ${total('pipePlurals')} pipe plurals ` +
    `turned into ICU, ${total('excludedKeys')} excluded top-level keys (email.*).`,
);
