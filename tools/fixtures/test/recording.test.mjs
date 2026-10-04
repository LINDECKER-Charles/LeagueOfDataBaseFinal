import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { Recording } from '../lib/recording.mjs';

const URL_A = 'https://ddragon.leagueoflegends.com/cdn/16.1.1/data/en_US/item.json';

/** A fetch answering from a status list, the last status repeated; counts its calls. */
function scripted(statuses, body = '{"data":{}}') {
  const calls = [];
  const fetchImpl = async (url) => {
    calls.push(url);
    const status = statuses[Math.min(calls.length - 1, statuses.length - 1)];
    return new Response(status === 200 ? body : null, {
      status,
      headers: { 'content-type': 'application/json' },
    });
  };
  return { fetchImpl, calls };
}

describe('Recording', () => {
  it('records a definitive absence as a status, without retrying it', async () => {
    const { fetchImpl, calls } = scripted([403]);
    const recording = new Recording({ fetchImpl });

    assert.equal(await recording.json(URL_A), null);

    assert.deepEqual(recording.entries.get(URL_A), { url: URL_A, status: 403 });
    assert.equal(calls.length, 1);
  });

  it('keeps a transient failure aside and records nothing for it', async () => {
    const { fetchImpl, calls } = scripted([503]);
    const recording = new Recording({ fetchImpl });

    assert.equal(await recording.json(URL_A), null);

    assert.equal(recording.entries.size, 0);
    assert.equal(recording.failures.length, 1);
    assert.equal(calls.length, 3);
  });

  it('fetches a URL once whichever phase asks, and stores the reduced body', async () => {
    const { fetchImpl, calls } = scripted([200], '{"data":{"1001":{},"1004":{}}}');
    const recording = new Recording({ fetchImpl });
    const onlyBoots = (file) => ({
      document: { data: { 1001: file.data['1001'] } },
      reduction: 'r',
    });

    await recording.json(URL_A);
    await recording.json(URL_A, onlyBoots);

    assert.equal(calls.length, 1);
    const entry = recording.entries.get(URL_A);
    assert.equal(entry.body.toString('utf8'), '{"data":{"1001":{}}}');
    assert.equal(entry.reduction, 'r');
  });

  it('records the status only when asked to', async () => {
    const { fetchImpl } = scripted([200], 'jpeg bytes');
    const recording = new Recording({ fetchImpl });

    await recording.status(URL_A, 'hotlinked');

    const entry = recording.entries.get(URL_A);
    assert.equal(entry.body, undefined);
    assert.equal(entry.bodyless, true);
    assert.equal(entry.reduction, 'hotlinked');
  });
});
