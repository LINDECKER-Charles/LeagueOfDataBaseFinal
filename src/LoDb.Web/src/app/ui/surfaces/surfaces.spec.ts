import { Location } from '@angular/common';
import { provideLocationMocks } from '@angular/common/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { Card } from './card';
import { EmptyState } from './empty-state';
import { Frame } from './frame';
import { Skeleton } from './skeleton';

@Component({
  imports: [Card, Frame, Skeleton],
  template: `
    <section id="plain" lodbFrame>plain</section>
    <section id="ornate" lodbFrame="ornate">ornate</section>
    <lodb-card id="card" eyebrow="Codex" heading="Ahri" frame="interactive">body</lodb-card>
    <lodb-skeleton id="tile" shape="tile" />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class SurfacesHost {}

@Component({
  imports: [EmptyState],
  template: `
    <lodb-empty-state id="bare" />
    <lodb-empty-state id="told" text="No runes for this version." />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class EmptyStateHost {}

const NO_RESULT = {
  common: { no_result: { text: 'No results found for your search.', back: '← Back' } },
  header: { navigation: { home: 'Home' } },
};

describe('surfaces', () => {
  function render(): HTMLElement {
    const fixture = TestBed.createComponent(SurfacesHost);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it.each([
    ['plain', ['hextech-frame']],
    ['ornate', ['hextech-frame', 'hx-corners']],
    ['card', ['block', 'p-5', 'hextech-frame', 'hextech-frame-hover']],
    ['tile', ['hx-sk', 'hx-sk-tile']],
  ])('gives #%s the classes %j', (id, expected) => {
    const classes = Array.from(render().querySelector(`#${id}`)?.classList ?? []);

    expect(classes).toEqual(expect.arrayContaining(expected));
  });

  it('renders the card eyebrow, heading and content', () => {
    const card = render().querySelector('#card');

    expect(card?.querySelector('.eyebrow')?.textContent).toBe('Codex');
    expect(card?.querySelector('h3')?.textContent).toBe('Ahri');
    expect(card?.textContent).toContain('body');
  });

  it('hides a skeleton from assistive technology', () => {
    expect(render().querySelector('#tile')?.getAttribute('aria-hidden')).toBe('true');
  });
});

describe('EmptyState', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
    document.documentElement.lang = 'fr';
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideLocationMocks(),
        provideTransloco({
          config: {
            availableLangs: ['en'],
            defaultLang: 'en',
            missingHandler: { logMissingKey: false },
            prodMode: true,
          },
          loader: class {
            getTranslation = () => of(NO_RESULT);
          },
        }),
      ],
    });
  });

  afterEach(() => {
    document.documentElement.lang = lang;
  });

  async function render(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(EmptyStateHost);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  function linesOf(element: Element | null): string[] {
    return [...(element?.querySelectorAll('p') ?? [])].map((p) => p.textContent?.trim() ?? '');
  }

  it('frames its "no results", says what is missing when told, and leads home', async () => {
    const host = await render();
    const told = host.querySelector('#told');

    expect(told?.classList).toContain('hextech-frame');
    expect(linesOf(told)).toEqual([
      'No results found for your search.',
      'No runes for this version.',
    ]);
    expect(linesOf(host.querySelector('#bare'))).toEqual(['No results found for your search.']);
    expect(told?.querySelector('a')?.getAttribute('href')).toBe('/fr');
    expect(told?.querySelector('a')?.textContent?.trim()).toBe('Home');
  });

  it('goes back in the history', async () => {
    const back = vi.spyOn(TestBed.inject(Location), 'back');
    const host = await render();

    host.querySelector<HTMLButtonElement>('#told button')?.click();

    expect(back).toHaveBeenCalledOnce();
  });

  it('sets its line in the direction of its own words, an English fallback too', async () => {
    const host = await render();

    expect(host.querySelector('#told p:last-of-type')?.getAttribute('dir')).toBe('auto');
  });
});
