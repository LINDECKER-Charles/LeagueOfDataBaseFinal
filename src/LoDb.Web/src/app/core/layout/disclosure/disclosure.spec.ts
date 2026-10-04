import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Disclosure } from './disclosure';
import { type DisclosureCue, disclosureClosing } from './disclosure-closing';

describe('disclosureClosing', () => {
  const folding: [cue: DisclosureCue, closes: boolean][] = [
    [{ kind: 'pointer', inside: false }, true],
    [{ kind: 'pointer', inside: true }, false],
    [{ kind: 'key', key: 'Escape' }, true],
    [{ kind: 'key', key: 'Enter' }, false],
    [{ kind: 'navigation' }, true],
  ];

  it.each(folding)('folds an open menu on %j: %s', (cue, closes) => {
    expect(disclosureClosing(true, cue)).toBe(closes);
  });

  it.each(folding)('leaves a closed menu alone on %j', (cue) => {
    expect(disclosureClosing(false, cue)).toBe(false);
  });
});

@Component({
  imports: [Disclosure],
  template: `
    <details lodbDisclosure open>
      <summary>Menu</summary>
      <a id="inside" href="#x">Entry</a>
    </details>
    <p id="outside">Page</p>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class DisclosureHost {}

describe('Disclosure', () => {
  function render(): HTMLElement {
    TestBed.configureTestingModule({ providers: [provideRouter([{ path: '**', children: [] }])] });
    const fixture = TestBed.createComponent(DisclosureHost);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function details(root: HTMLElement): HTMLDetailsElement {
    return root.querySelector('details') as HTMLDetailsElement;
  }

  it('folds on a press outside, not inside', () => {
    const root = render();

    root.querySelector('#inside')?.dispatchEvent(new Event('pointerdown', { bubbles: true }));
    expect(details(root).open).toBe(true);

    root.querySelector('#outside')?.dispatchEvent(new Event('pointerdown', { bubbles: true }));
    expect(details(root).open).toBe(false);
  });

  it('folds on Escape and hands focus back to the summary', () => {
    const root = render();
    document.body.appendChild(root);

    root
      .querySelector('#inside')
      ?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));

    expect(details(root).open).toBe(false);
    expect(document.activeElement).toBe(root.querySelector('summary'));
    root.remove();
  });

  it('folds once a navigation completes', async () => {
    const root = render();

    await TestBed.inject(Router).navigateByUrl('/elsewhere');

    expect(details(root).open).toBe(false);
  });
});
