// `node tools/next/fixtures/record.mjs`: records the Data Dragon and CommunityDragon answers
// the .NET ingestion tests replay (tests/fixtures/ddragon, replayed by
// tests/LoDb.Testing/Fixtures). Needs network access to ddragon.leagueoflegends.com and
// raw.communitydragon.org. The recording is all or nothing: one transient failure, a size over
// the budget or an error leaves the previous recording in place.
// Tests of the reductions and the layout: `node --test "tools/next/fixtures/**/*.test.mjs"`.
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { recordAll } from './lib/record-all.mjs';
import { layOut, SIZE_BUDGET_BYTES, writeFixtures } from './lib/write-fixtures.mjs';

const REPO_ROOT = fileURLToPath(new URL('../../../', import.meta.url));
const FIXTURE_DIR = join(REPO_ROOT, 'tests', 'fixtures', 'ddragon');
const RECORDING_DAY_LENGTH = 10;

const { recording, roles } = await recordAll();
if (recording.failures.length > 0) {
  for (const failure of recording.failures) {
    console.error(`transient: ${failure.message}`);
  }
  console.error(`fixtures: ${recording.failures.length} transient failures, nothing written.`);
  process.exit(1);
}

const recordedOn = new Date().toISOString().slice(0, RECORDING_DAY_LENGTH);
const layout = layOut(recording.entries, { recordedOn, roles });
writeFixtures(FIXTURE_DIR, layout);

const statuses = Map.groupBy(recording.entries.values(), (entry) => entry.status);
const counts = [...statuses].map(([status, entries]) => `${entries.length} × ${status}`);
console.log(
  `fixtures: ${recording.entries.size} responses (${counts.join(', ')}), ` +
    `${layout.files.size} files, ${layout.size} bytes of ${SIZE_BUDGET_BYTES}; ` +
    `latest ${roles.latest}, previous ${roles.previous}.`,
);
