import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DialogFrame } from './dialog-frame';

@Component({
  imports: [DialogFrame],
  template: `
    <lodb-dialog [eyebrow]="eyebrow()" heading="Title" headingId="frame-title" closeLabel="Close">
      @if (lead()) {
        <button lodbDialogLead type="button">Back</button>
      }
      <p>Body</p>
    </lodb-dialog>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class FrameHost {
  readonly eyebrow = signal<string | null>(null);
  readonly lead = signal(false);
}

async function render(eyebrow: string | null, lead = false): Promise<HTMLElement> {
  const fixture = TestBed.createComponent(FrameHost);
  fixture.componentInstance.eyebrow.set(eyebrow);
  fixture.componentInstance.lead.set(lead);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

describe('lodb-dialog', () => {
  it('sets the heading alone as an eyebrow, named by its id', async () => {
    const host = await render(null);
    const heading = host.querySelector('h2');

    expect(heading?.id).toBe('frame-title');
    expect(heading?.classList).toContain('eyebrow');
    expect(host.querySelector('.hx-dialog-panel__head--titled')).toBeNull();
  });

  it('puts an eyebrow above a display title, with the larger close', async () => {
    const host = await render('Contact form');
    const head = host.querySelector('.hx-dialog-panel__head--titled');

    expect(head?.querySelector('p.eyebrow')?.textContent).toBe('Contact form');
    expect(head?.querySelector('p + h2')?.id).toBe('frame-title');
    expect(head?.querySelector('h2')?.classList).not.toContain('eyebrow');
    expect(head?.querySelector('button')?.classList).toContain('hx-dialog-panel__close--large');
    expect(host.querySelector('lodb-dialog > p')?.textContent).toBe('Body');
  });

  it('leads the head with the element marked for it, before the heading', async () => {
    const host = await render(null, true);
    const head = host.querySelector('.hx-dialog-panel__head');

    expect([...(head?.children ?? [])].map((child) => child.tagName)).toEqual([
      'BUTTON',
      'H2',
      'BUTTON',
    ]);
    expect(head?.firstElementChild?.textContent).toBe('Back');
    expect(host.querySelector('lodb-dialog > p')?.textContent).toBe('Body');
  });
});
