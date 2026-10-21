import type { AdminText } from '../../../../../shared/inject-admin-text';
import { prerenderProgress } from '../../../../../testing/fixtures/prerender-progress';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import { type ConsequenceContext, consequencesOf } from './consequences-of';

// The key and its parameters, as a spec reads them.
const TEXTS: AdminText = {
  text: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key),
  term: (_group, name) => name,
};
const CONFIRM = 'prerender.live.confirm';
const PROGRESS = prerenderProgress();

function context(changes: Partial<ConsequenceContext> = {}): ConsequenceContext {
  return {
    pass: PROGRESS.pass,
    running: PROGRESS.running,
    report: prerenderReport(),
    ...changes,
  };
}

function keys(lines: readonly string[]): string[] {
  return lines.map((line) => line.split(' ')[0].replace(`${CONFIRM}.`, ''));
}

describe('consequencesOf', () => {
  it('says where a pause stops the unit running, and the promotions that wait', () => {
    const data = consequencesOf('pause', context(), TEXTS);

    expect(keys(data.lines)).toEqual([
      'pause_current',
      'pause_resume',
      'pause_promotions',
      'pause_slot',
    ]);
    expect(data.lines[0]).toContain('"done":"312","total":"1 140"');
    expect(data.title).toBe(`${CONFIRM}.pause_title {"id":12}`);
    expect(data.tone).toBe('gold');
  });

  it('pauses without a unit running, nor promotions', () => {
    const report = prerenderReport({ enqueuesPromotions: false });
    const data = consequencesOf('pause', context({ running: [], report }), TEXTS);

    expect(keys(data.lines)).toEqual(['pause_resume', 'pause_slot']);
  });

  it('warns that a pass paused by itself pauses again without a fix', () => {
    const reason = 'The store refused the writer key (403).';
    const pass = { ...PROGRESS.pass, state: 'paused', pauseReason: reason };
    const data = consequencesOf('resume', context({ pass }), TEXTS);

    expect(keys(data.lines)).toEqual(['resume_reason', 'resume_warning']);
    expect(data.lines[0]).toContain(reason);
    expect(data.confirmLabel).toBe(`${CONFIRM}.resume_confirm`);
  });

  it('counts what a cancel leaves, and how partial the epoch stays', () => {
    const data = consequencesOf('cancel', context(), TEXTS);

    expect(keys(data.lines)).toEqual([
      'cancel_left',
      'cancel_kept',
      'cancel_partial',
      'cancel_later',
    ]);
    expect(data.lines[0]).toContain('"units":1410');
    expect(data.lines[1]).toContain('"pages":1512300,"size":"58.98 GB"');
    expect(data.lines[2]).toContain('"epoch":2,"share":"98 %"');
    expect(data.tone).toBe('danger');
    expect(data.keepLabel).toBe(`${CONFIRM}.cancel_keep`);
  });

  it('leaves the coverage out of an epoch not measured', () => {
    const pass = { ...PROGRESS.pass, epoch: 1 };

    expect(keys(consequencesOf('cancel', context({ pass }), TEXTS).lines)).not.toContain(
      'cancel_partial',
    );
  });
});
