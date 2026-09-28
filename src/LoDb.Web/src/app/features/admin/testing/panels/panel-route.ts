import type { Type } from '@angular/core';
import type { Route } from '@angular/router';

/** The route of a panel: `panel` itself, or its component routed at the path of `url`. */
export function panelRoute(panel: Type<unknown> | Route, url: string): Route {
  const [path = ''] = url.slice(1).split('?');
  return typeof panel === 'function' ? { path, component: panel } : panel;
}
