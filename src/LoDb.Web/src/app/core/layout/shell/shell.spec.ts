import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Shell } from './shell';

// Slots given in a shuffled order: the envelope, not the caller, decides where they land.
@Component({
  imports: [Shell],
  template: `
    <lodb-shell>
      <p id="page">page</p>
      <span id="contact" lodbSlot="contact">contact</span>
      <span id="banner" lodbSlot="banner">banner</span>
      <span id="account" lodbSlot="account">account</span>
      <span id="switcher" lodbSlot="switcher">switcher</span>
    </lodb-shell>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class ShellHost {}

describe('Shell', () => {
  function render(): HTMLElement {
    const fixture = TestBed.createComponent(ShellHost);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('puts the context switcher then the account menu in the header', () => {
    const header = render().querySelector('lodb-shell > header');

    expect(Array.from(header?.children ?? [], (child) => child.id)).toEqual([
      'switcher',
      'account',
    ]);
  });

  it('puts the banner between the header and the page', () => {
    const banner = render().querySelector('lodb-shell > #banner');

    expect(banner?.previousElementSibling?.tagName).toBe('HEADER');
    expect(banner?.nextElementSibling?.tagName).toBe('MAIN');
  });

  it('renders the page, and only the page, in main', () => {
    const main = render().querySelector('lodb-shell > main');

    expect(Array.from(main?.children ?? [], (child) => child.id)).toEqual(['page']);
  });

  it('puts the contact entry in the footer', () => {
    const footer = render().querySelector('lodb-shell > footer');

    expect(footer?.querySelector('#contact')).not.toBeNull();
  });
});
