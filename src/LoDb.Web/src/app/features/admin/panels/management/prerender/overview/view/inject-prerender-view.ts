import { type Signal, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PRERENDER_VIEWS, type PrerenderView } from './prerender-view';

/** The view on screen, and the way to show another. */
export interface ViewChoice {
  readonly view: Signal<PrerenderView>;
  /** Shows `view` at once, and writes it in the URL in place of the one shown. */
  show(view: PrerenderView): void;
}

const PARAM = 'view';
const FIRST_VIEW: PrerenderView = 'pass';

function isView(value: string | null): value is PrerenderView {
  return (PRERENDER_VIEWS as readonly (string | null)[]).includes(value);
}

/**
 * The view of the page, kept in the `view` parameter of the URL so that a reload or a shared
 * link opens it again; the active pass's view when the parameter names none, which it then
 * leaves out. The view changes at once, so that the focus can go into it; the URL follows,
 * replaced rather than added to the history: the arrows of the tabs move from view to view,
 * and « Back » leaves the page as it came.
 */
export function injectPrerenderView(): ViewChoice {
  const route = inject(ActivatedRoute);
  const router = inject(Router);
  const asked = route.snapshot.queryParamMap.get(PARAM);
  const view = signal<PrerenderView>(isView(asked) ? asked : FIRST_VIEW);
  return {
    view: view.asReadonly(),
    show: (next) => {
      view.set(next);
      void router.navigate([], {
        relativeTo: route,
        queryParams: { [PARAM]: next === FIRST_VIEW ? null : next },
        queryParamsHandling: 'merge',
        replaceUrl: true,
      });
    },
  };
}
