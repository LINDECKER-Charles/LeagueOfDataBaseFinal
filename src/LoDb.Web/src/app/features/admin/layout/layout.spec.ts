import { Component, type Type } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Seo } from '../../../core/seo/seo';
import { configureAdminTestBed } from '../testing/admin-test-bed';
import { AdminBand } from './admin-band';
import { AdminCard } from './admin-card';
import { AdminNotices } from './admin-notices';
import { AdminRule } from './admin-rule';
import { NoticesOutlet } from './notices-outlet';
import { PageHead } from './page-head';

@Component({ template: '' })
class Blank {}

function render<T>(type: Type<T>, inputs: Readonly<Record<string, unknown>>): ComponentFixture<T> {
  const fixture = TestBed.createComponent(type);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();
  return fixture;
}

function root(fixture: ComponentFixture<unknown>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function words(element: Element | null | undefined): string {
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

describe('admin layout', () => {
  const apply = vi.fn(() => Promise.resolve());

  beforeEach(() => {
    apply.mockClear();
    configureAdminTestBed(
      [{ path: 'elsewhere', component: Blank }],
      [{ provide: Seo, useValue: { apply } }],
    );
  });

  it('titles the document as a private admin page, once the title is known', () => {
    const fixture = render(PageHead, { eyebrow: 'Gestion', title: '' });
    TestBed.tick();
    expect(apply).not.toHaveBeenCalled();

    fixture.componentRef.setInput('title', 'Comptes');
    fixture.componentRef.setInput('subtitle', 'Tous les comptes');
    fixture.detectChanges();
    TestBed.tick();

    expect(root(fixture).querySelector('h1')?.textContent?.trim()).toBe('Comptes');
    expect(words(root(fixture))).toContain('Tous les comptes');
    expect(apply).toHaveBeenCalledWith({
      kind: 'private',
      titleFormat: 'admin',
      locale: 'fr',
      title: 'Comptes',
    });
  });

  it('titles the document with the shorter legacy title when the page has one', () => {
    render(PageHead, {
      eyebrow: 'Gestion',
      title: 'Messages de contact',
      documentTitle: 'Messages',
    });
    TestBed.tick();

    expect(apply).toHaveBeenLastCalledWith(expect.objectContaining({ title: 'Messages' }));
  });

  it('heads a card with its title and what it counts, and frames a bare table', () => {
    const fixture = render(AdminCard, { heading: 'Pages', chip: 'vues' });

    expect(words(root(fixture).querySelector('h2'))).toBe('Pages');
    expect(words(root(fixture).querySelector('[lodbChip]'))).toBe('vues');
    expect(root(render(AdminCard, {})).querySelectorAll('h2')).toHaveLength(0);
  });

  it('labels a section rule', () => {
    expect(words(root(render(AdminRule, { label: 'Trafic' })))).toBe('Trafic');
  });

  it('announces an alert at once and a notice politely', () => {
    const fixture = render(AdminBand, { tone: 'alert' });
    expect(root(fixture).getAttribute('role')).toBe('alert');

    fixture.componentRef.setInput('tone', 'notice');
    fixture.detectChanges();
    expect(root(fixture).getAttribute('role')).toBe('status');
    expect(root(fixture).classList).toContain('notice');
  });

  it('shows the last outcome of an action until the next page', async () => {
    const notices = TestBed.inject(AdminNotices);
    const fixture = render(NoticesOutlet, {});

    notices.post({ tone: 'notice', text: 'Banni.' });
    notices.post({ tone: 'alert', text: 'Refusé.' });
    fixture.detectChanges();
    const band = root(fixture).querySelector('lodb-admin-band');
    expect(band?.getAttribute('role')).toBe('alert');
    expect(words(root(fixture))).toBe('Refusé.');

    await TestBed.inject(Router).navigateByUrl('/elsewhere');
    fixture.detectChanges();
    expect(notices.current()).toBeNull();
    expect(root(fixture).querySelector('lodb-admin-band')).toBeNull();
  });

  it('forgets the last outcome once the panels are left, for the next session', () => {
    const notices = TestBed.inject(AdminNotices);
    const fixture = render(NoticesOutlet, {});
    notices.post({ tone: 'notice', text: 'Banni.' });

    fixture.destroy();

    expect(notices.current()).toBeNull();
  });
});
