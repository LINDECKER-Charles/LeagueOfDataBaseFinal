import { DIALOG_DATA } from '@angular/cdk/dialog';
import { type WritableSignal, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { WarmUpProgress } from '../../../api/generated/models/warm-up-progress';
import { preparingFrame } from '../preparing-frame';
import { LoaderDialog } from './loader-dialog';

const EN = {
  loader: {
    eyebrow: 'Data Dragon',
    title: 'Summoning data',
    subtitle: 'Fetching resources from the Rift…',
    preparing: 'Preparing datasets…',
    status: { fetching: 'fetching', ready: 'ready' },
  },
  header: { navigation: { champion: 'Champions', item: 'Items' } },
};
const PREPARING = preparingFrame(['champions', 'items']);
const IMAGES: WarmUpProgress = {
  stage: 'images',
  total: 4,
  settled: 1,
  resources: [
    { resource: 'champions', total: 0, settled: 0, ready: true },
    { resource: 'items', total: 4, settled: 1, ready: false },
  ],
  latest: { name: 'Doran’s Blade', resource: 'items' },
};

interface Rendered {
  readonly fixture: ComponentFixture<LoaderDialog>;
  readonly host: HTMLElement;
  readonly frame: WritableSignal<WarmUpProgress>;
}

async function render(): Promise<Rendered> {
  const frame = signal(PREPARING);
  TestBed.configureTestingModule({
    providers: [
      { provide: DIALOG_DATA, useValue: frame.asReadonly() },
      provideTransloco({
        config: {
          availableLangs: ['en'],
          defaultLang: 'en',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = () => of(EN);
        },
      }),
    ],
  });
  const fixture = TestBed.createComponent(LoaderDialog);
  await fixture.whenStable();
  return { fixture, host: fixture.nativeElement as HTMLElement, frame };
}

function text(host: HTMLElement, selector: string): string[] {
  return [...host.querySelectorAll(selector)].map((node) => node.textContent?.trim() ?? '');
}

function bar(host: HTMLElement): HTMLElement {
  return host.querySelector('[role=progressbar]') as HTMLElement;
}

describe('LoaderDialog', () => {
  it('names the lists of the destination, none ready while the datasets are prepared', async () => {
    const { host } = await render();

    expect(host.querySelector('h2')?.id).toBe(LoaderDialog.HEADING_ID);
    expect(text(host, '.hx-loader__subtitle')).toEqual(['Preparing datasets…']);
    expect(text(host, '.hx-row')).toEqual([
      expect.stringMatching(/^Champions\s+fetching$/),
      expect.stringMatching(/^Items\s+fetching$/),
    ]);
    expect(bar(host).classList).toContain('hx-bar--indeterminate');
    expect(bar(host).hasAttribute('aria-valuenow')).toBe(false);
    expect(text(host, '.hx-now__name')).toEqual([]);
  });

  it('turns a list ready once its images have landed, the bar at the share fetched', async () => {
    const { fixture, host, frame } = await render();

    frame.set(IMAGES);
    await fixture.whenStable();

    expect(text(host, '.hx-row--ready .hx-row__label')).toEqual(['Champions']);
    expect(text(host, '.hx-loader__subtitle')).toEqual(['Fetching resources from the Rift…']);
    expect(bar(host).getAttribute('aria-valuenow')).toBe('25');
    expect(text(host, '.hx-bar__pct')).toEqual(['25%']);
    expect(text(host, '.hx-now__name')).toEqual(['Doran’s Blade']);
    expect(text(host, '.hx-now__category')).toEqual(['Items']);
  });
});
