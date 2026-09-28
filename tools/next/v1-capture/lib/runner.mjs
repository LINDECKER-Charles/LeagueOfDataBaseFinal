// Plays the steps of one group against the running go-api and records each exchange.
// node:http rather than fetch: fetch trims header values and adds its own headers,
// which would change the very requests the scenarios are about.
import http from 'node:http';
import { hideStorage, psql, sleep, stopDatabase } from './environment.mjs';
import { normalizeResponse } from './normalize.mjs';
import { expandSteps, resolveRequest } from './scenarios.mjs';
import { timings } from './settings.mjs';

const millisecondsPerSecond = 1000;
const nowSeconds = () => Math.floor(Date.now() / millisecondsPerSecond);

/**
 * Sends one request exactly as written and resolves with its raw response.
 * @returns {Promise<{ status: number, headers: object, text: string, window: object }>}
 */
export function send(port, request) {
  const from = nowSeconds();
  return new Promise((resolve, reject) => {
    const outgoing = http.request({
      host: '127.0.0.1', port, method: request.method, path: request.path,
      headers: request.headers, agent: false, timeout: timings.requestTimeoutMs,
    }, (response) => {
      const chunks = [];
      response.on('data', (chunk) => chunks.push(chunk));
      response.on('end', () => resolve({
        status: response.statusCode,
        headers: response.headers,
        text: Buffer.concat(chunks).toString('utf8'),
        window: { from, to: nowSeconds() },
      }));
    });
    outgoing.on('timeout', () => outgoing.destroy(new Error(`timeout on ${request.path}`)));
    outgoing.on('error', reject);
    outgoing.end();
  });
}

/** Applies a non-HTTP step (clock, database or storage change). */
async function perform(step) {
  switch (step.action) {
    case 'sleep':
      await sleep(step.ms);
      break;
    case 'sql':
      psql(step.sql);
      break;
    case 'stopDatabase':
      stopDatabase();
      break;
    case 'hideStorage':
      hideStorage();
      break;
    default:
      throw new Error(`unknown action ${step.action}`);
  }
}

async function record(step, context) {
  const raw = await send(context.port, resolveRequest(step.request, context.keys));
  const { volatile, ...definition } = step;
  return { ...definition, response: normalizeResponse(raw, volatile) };
}

/**
 * Runs every step of a group in order; actions are kept in the timeline so a reference
 * file alone is enough to replay the group.
 * @param {{ port: number, keys: object }} context
 */
export async function runGroup(group, context) {
  const exchanges = [];
  for (const step of expandSteps(group.steps)) {
    if (step.action) {
      await perform(step);
      exchanges.push(step);
    } else {
      exchanges.push(await record(step, context));
    }
  }
  return exchanges;
}
