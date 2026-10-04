// Evaluates JavaScript in the page the app shows right now. A new connection each time: the
// page changes under the gate (cold starts, the plugin's rollback reload).
import { adb, pidOf } from './adb.mjs';
import { appPageOf, devtoolsSocketOf, evaluationResult } from './devtools.mjs';

// Host side of the adb forward; nothing else of the runner listens there.
const DEVTOOLS_PORT = 9333;
const EVALUATE_ID = 1;

function forwardDevtools() {
  const pid = pidOf();
  const socket = pid && devtoolsSocketOf(adb('shell', 'cat', '/proc/net/unix'), pid);
  if (!socket) {
    throw new Error('the WebView of the app exposes no DevTools socket yet');
  }
  adb('forward', `tcp:${DEVTOOLS_PORT}`, `localabstract:${socket}`);
}

function evaluateOn(url, expression, timeoutMs) {
  return new Promise((resolve, reject) => {
    const socket = new WebSocket(url);
    const timer = setTimeout(() => {
      socket.close();
      reject(new Error(`no answer within ${timeoutMs} ms to ${expression}`));
    }, timeoutMs);
    const settle = (settler, value) => {
      clearTimeout(timer);
      socket.close();
      settler(value);
    };
    socket.addEventListener('error', () => settle(reject, new Error('DevTools socket error')));
    socket.addEventListener('open', () => {
      const params = { expression, awaitPromise: true, returnByValue: true };
      socket.send(JSON.stringify({ id: EVALUATE_ID, method: 'Runtime.evaluate', params }));
    });
    socket.addEventListener('message', ({ data }) => {
      const response = JSON.parse(data);
      if (response.id !== EVALUATE_ID) {
        return;
      }
      try {
        settle(resolve, evaluationResult(response));
      } catch (error) {
        settle(reject, error);
      }
    });
  });
}

/** Evaluates the expression (a promise is awaited) and returns its JSON value. */
export async function evaluateInApp(expression, timeoutMs) {
  forwardDevtools();
  const targets = await (await fetch(`http://127.0.0.1:${DEVTOOLS_PORT}/json/list`)).json();
  const page = appPageOf(targets);
  if (page === null) {
    throw new Error(`no page of the app among ${targets.length} DevTools targets`);
  }
  return evaluateOn(page.webSocketDebuggerUrl, expression, timeoutMs);
}
