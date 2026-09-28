import { ViewportScroller } from '@angular/common';
import {
  EnvironmentInjector,
  createEnvironmentInjector,
  runInInjectionContext,
} from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { anchorOffsetOf } from './anchor-offset-of';
import { injectAnchorOffset } from './inject-anchor-offset';

const TARGET_ID = 'rune-Électrocution';
const MARGIN = '88px';

/** A page whose only element is the target, with the scroll margin foundation/base.css sets. */
function pageAt(hash: string): Document {
  const target = { id: TARGET_ID };
  return {
    location: { hash },
    getElementById: (id: string) => (id === TARGET_ID ? target : null),
    defaultView: {
      getComputedStyle: (element: unknown) => ({
        scrollMarginBlockStart: element === target ? MARGIN : '0px',
      }),
    },
  } as unknown as Document;
}

describe('anchorOffsetOf', () => {
  it("keeps the scroll margin of the fragment's target above it", () => {
    expect(anchorOffsetOf(pageAt(`#${encodeURIComponent(TARGET_ID)}`))).toEqual([0, 88]);
  });

  it('keeps nothing without a fragment, or for one naming no element or malformed', () => {
    expect(anchorOffsetOf(pageAt(''))).toEqual([0, 0]);
    expect(anchorOffsetOf(pageAt('#rune-Unknown'))).toEqual([0, 0]);
    expect(anchorOffsetOf(pageAt('#rune-%E0%A4%A'))).toEqual([0, 0]);
  });
});

describe('injectAnchorOffset', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    history.replaceState(null, '', '/');
  });

  it("holds the router's scroller to the target's margin while the page lives", () => {
    const target = document.createElement('article');
    target.id = TARGET_ID;
    document.body.append(target);
    vi.spyOn(window, 'getComputedStyle').mockReturnValue({
      scrollMarginBlockStart: MARGIN,
    } as CSSStyleDeclaration);
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => undefined);
    history.replaceState(null, '', `/en/runes/8100#${encodeURIComponent(TARGET_ID)}`);
    const page = createEnvironmentInjector([], TestBed.inject(EnvironmentInjector));
    const scroller = TestBed.inject(ViewportScroller);
    const top = target.getBoundingClientRect().top + window.scrollY;

    runInInjectionContext(page, injectAnchorOffset);
    scroller.scrollToAnchor(TARGET_ID);
    expect(scrollTo).toHaveBeenLastCalledWith(expect.objectContaining({ top: top - 88 }));

    page.destroy();
    scroller.scrollToAnchor(TARGET_ID);
    expect(scrollTo).toHaveBeenLastCalledWith(expect.objectContaining({ top }));
    target.remove();
  });
});
