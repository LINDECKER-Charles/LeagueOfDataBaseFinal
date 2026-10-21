import { TestBed } from '@angular/core/testing';
import { configureAdminTestBed } from '../../../../testing/admin-test-bed';
import { type BarSegment, StackedBar } from './stacked-bar';

const SEGMENTS: BarSegment[] = [
  { key: 'written', value: 3, color: 'var(--color-good)', label: 'écrites' },
  { key: 'failed', value: 0, color: 'var(--color-bad)', label: 'en échec' },
  { key: 'left', value: 1, color: 'var(--color-track)', label: 'restantes' },
];

function render(inputs: Readonly<Record<string, unknown>>): HTMLElement {
  const fixture = TestBed.createComponent(StackedBar);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('StackedBar', () => {
  beforeEach(() => configureAdminTestBed());

  it('gives each segment its share of the width, and leaves out a segment of nothing', () => {
    const bar = render({ segments: SEGMENTS, label: '75 %' });
    const segments = [...bar.querySelectorAll<HTMLElement>('[data-segment]')];

    expect(segments.map((segment) => segment.dataset['segment'])).toEqual(['written', 'left']);
    expect(segments.map((segment) => segment.style.inlineSize)).toEqual(['75%', '25%']);
    expect(segments[0]?.style.background).toBe('var(--color-good)');
  });

  it('is one image named by its label, sized and muted on demand', () => {
    const bar = render({
      segments: SEGMENTS,
      label: '75 % : 3 écrites',
      size: 'hero',
      isMuted: true,
    });
    const image = bar.querySelector('[role="img"]');

    expect(image?.getAttribute('aria-label')).toBe('75 % : 3 écrites');
    expect(image?.getAttribute('data-size')).toBe('hero');
    expect(image?.classList).toContain('bar--muted');
  });
});
