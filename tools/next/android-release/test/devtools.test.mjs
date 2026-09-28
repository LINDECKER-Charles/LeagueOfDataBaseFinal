import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { appPageOf, devtoolsSocketOf, evaluationResult } from '../gate/lib/devtools.mjs';
import { poll } from '../gate/lib/wait.mjs';

const PROC_NET_UNIX = `Num       RefCount Protocol Flags    Type St Inode Path
0000000000000000: 00000002 00000000 00010000 0001 01 51234 @webview_devtools_remote_4242
0000000000000000: 00000002 00000000 00010000 0001 01 51235 @chrome_devtools_remote
0000000000000000: 00000002 00000000 00010000 0001 01 51236 @webview_devtools_remote_42`;

describe('devtoolsSocketOf', () => {
  it('finds the socket of the process', () => {
    assert.equal(devtoolsSocketOf(PROC_NET_UNIX, '42'), 'webview_devtools_remote_42');
    assert.equal(devtoolsSocketOf(PROC_NET_UNIX, '4242'), 'webview_devtools_remote_4242');
  });

  it('finds nothing before the WebView opens it', () => {
    assert.equal(devtoolsSocketOf(PROC_NET_UNIX, '424'), null);
  });
});

describe('appPageOf', () => {
  const page = {
    type: 'page',
    url: 'https://localhost/',
    webSocketDebuggerUrl: 'ws://127.0.0.1:9333/devtools/page/1',
  };

  it('picks the page Capacitor serves', () => {
    const worker = { ...page, type: 'service_worker' };
    const blank = { ...page, url: 'about:blank' };

    assert.equal(appPageOf([worker, blank, page]), page);
  });

  it('skips a page another client is attached to', () => {
    assert.equal(appPageOf([{ ...page, webSocketDebuggerUrl: undefined }]), null);
  });
});

describe('evaluationResult', () => {
  it('gives the value of the evaluation', () => {
    const response = { id: 1, result: { result: { type: 'object', value: { bundleId: null } } } };

    assert.deepEqual(evaluationResult(response), { bundleId: null });
  });

  it('throws the rejection of the plugin', () => {
    const response = {
      id: 1,
      result: {
        result: { type: 'object' },
        exceptionDetails: {
          text: 'Uncaught (in promise)',
          exception: { description: 'Error: Signature verification failed.' },
        },
      },
    };

    assert.throws(() => evaluationResult(response), /Signature verification failed/);
  });

  it('throws an error of the protocol', () => {
    assert.throws(
      () => evaluationResult({ id: 1, error: { message: 'Cannot find context' } }),
      /Cannot find context/,
    );
  });
});

describe('poll', () => {
  it('waits until the check holds, failures meaning not yet', async () => {
    let calls = 0;

    await poll(
      async () => {
        calls++;
        if (calls === 1) {
          throw new Error('restarting');
        }
        return calls === 2;
      },
      { timeoutMs: 5_000, what: 'the app' },
    );

    assert.equal(calls, 2);
  });

  it('names what it waited for, with the last failure', async () => {
    await assert.rejects(
      poll(
        async () => {
          throw new Error('no socket');
        },
        { timeoutMs: 1, what: 'the rollback' },
      ),
      /the rollback \(last failure: no socket\)/,
    );
  });
});
