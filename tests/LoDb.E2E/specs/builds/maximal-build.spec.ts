import { buildParts, unsafeHeaders } from '../builds-share/builds';
import { expect, test } from '../../support/worker-account';

// The bounds of a build (LoDb.Domain BuildLimits): texts count characters, not bytes.
const NAME_MAX = 80;
const DESCRIPTION_MAX = 2000;
const STEPS_MAX = 10;
const STEP_LABEL_MAX = 40;
const STEP_NOTE_MAX = 300;
const ITEMS_PER_STEP = 4;

// A character outside the Basic Multilingual Plane: one character for the rules, a surrogate
// pair in UTF-16, twelve bytes once escaped.
const WIDEST_CHARACTER = '\u{1F48E}';
const KIB = 1024;
const CREATED = 201;
const OK = 200;

const text = (length: number) => WIDEST_CHARACTER.repeat(length);

// The longest way JSON allows to write a body: every non-ASCII UTF-16 unit as a \u escape,
// as clients that keep to ASCII send it (System.Text.Json's default, Python's json.dumps).
function escapedJson(value: unknown): string {
  return JSON.stringify(value).replace(
    /[\u007f-￿]/g,
    (unit) => `\\u${unit.charCodeAt(0).toString(16).padStart(4, '0')}`,
  );
}

test('saves a build with every text at its longest and every item it may hold', async ({
  member,
}) => {
  test.slow();
  const parts = await buildParts(member, 'sr');
  const items = Array.from(
    { length: ITEMS_PER_STEP },
    (_, index) => parts.items[index % parts.items.length]?.id ?? '',
  );
  const build = {
    name: text(NAME_MAX),
    description: text(DESCRIPTION_MAX),
    isPublic: true,
    gameMode: 'sr',
    language: 'en_US',
    structure: {
      championId: parts.champion.id,
      runes: parts.runes,
      steps: Array.from({ length: STEPS_MAX }, () => ({
        label: text(STEP_LABEL_MAX),
        note: text(STEP_NOTE_MAX),
        items,
      })),
    },
  };
  const body = escapedJson(build);
  const bytes = Buffer.byteLength(body, 'utf8');
  test.info().annotations.push({ type: 'request body', description: `${bytes} bytes` });
  // The reason the API reads up to 128 KiB: the largest legitimate body outgrows 64 KiB.
  expect(bytes).toBeGreaterThan(64 * KIB);
  const headers = { ...(await unsafeHeaders(member)), 'Content-Type': 'application/json' };

  const created = await member.request.post('/api/builds', { headers, data: body });
  expect(created.status(), await created.text()).toBe(CREATED);
  const saved = (await created.json()) as { id: number; name: string };
  expect(saved.name).toBe(build.name);

  const replaced = await member.request.put(`/api/builds/${saved.id}`, { headers, data: body });
  expect(replaced.status(), await replaced.text()).toBe(OK);
});
