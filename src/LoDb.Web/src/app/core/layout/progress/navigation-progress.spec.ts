import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { NavigationProgress } from './navigation-progress';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Page {}

// A route whose data takes `slow` milliseconds to resolve.
async function progressWith(slow: number) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        {
          path: 'slow',
          component: Page,
          resolve: { data: () => new Promise((resolve) => setTimeout(resolve, slow)) },
        },
        { path: '**', component: Page },
      ]),
    ],
  });
  const harness = await RouterTestingHarness.create('/');
  const fixture = TestBed.createComponent(NavigationProgress);
  fixture.detectChanges();
  const bar = () => (fixture.nativeElement as HTMLElement).querySelector('.hx-nav-progress');
  const navigation = TestBed.inject(Router).navigateByUrl('/slow');
  return { fixture, harness, bar, navigation };
}

describe('NavigationProgress', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('stays out of a navigation shorter than half a second', async () => {
    const { fixture, bar, navigation } = await progressWith(300);

    await vi.advanceTimersByTimeAsync(300);
    await navigation;
    fixture.detectChanges();

    expect(bar()).toBeNull();
    await vi.advanceTimersByTimeAsync(1000);
    fixture.detectChanges();
    expect(bar()).toBeNull();
  });

  it('creeps on through a slow navigation, then completes and goes', async () => {
    const { fixture, bar, navigation } = await progressWith(2000);

    await vi.advanceTimersByTimeAsync(600);
    fixture.detectChanges();
    expect(bar()?.classList).toContain('hx-nav-progress--pending');

    await vi.advanceTimersByTimeAsync(1400);
    await navigation;
    fixture.detectChanges();
    expect(bar()?.classList).toContain('hx-nav-progress--done');

    await vi.advanceTimersByTimeAsync(400);
    fixture.detectChanges();
    expect(bar()).toBeNull();
  });
});
