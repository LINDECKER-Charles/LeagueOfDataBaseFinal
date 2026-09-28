// `npm run i18n:report` (src/LoDb.Web): completeness of the Transloco catalogues against
// `en`, written to docs/reecriture/rapports/i18n-completude.md. Never blocking: it exits 0
// whatever the gaps, and only fails when the catalogues cannot be read at all.
import { appendFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, relative } from 'node:path';
import { I18N_PATHS } from './lib/i18n-paths.mjs';
import { runReport } from './lib/report/run-report.mjs';

const { markdown, summary } = runReport(I18N_PATHS.catalogueDir);

mkdirSync(dirname(I18N_PATHS.reportFile), { recursive: true });
writeFileSync(I18N_PATHS.reportFile, markdown);
// On GitHub Actions the report also lands on the job summary, where the CI shows it.
if (process.env.GITHUB_STEP_SUMMARY) {
  appendFileSync(process.env.GITHUB_STEP_SUMMARY, markdown);
}
for (const line of summary) {
  console.log(line);
}
console.log(
  `i18n:report: ${summary.length} incomplete catalogues, ` +
    `report written to ${relative(I18N_PATHS.repoRoot, I18N_PATHS.reportFile)}.`,
);
