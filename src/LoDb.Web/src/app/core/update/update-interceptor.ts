import type { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { tap } from 'rxjs';
import { ClientUpdate } from './client-update';
import { updateRequirementOf } from './update-requirement-of';

/**
 * Catches the `426 Upgrade Required` the API answers to an app below the minimum version of
 * the client policy (L9.0), to show the blocking update screen. The error still reaches the
 * caller, whose page the screen covers. The web sends no `X-LoDb-Client` header and never
 * gets a 426.
 */
export const updateInterceptor: HttpInterceptorFn = (request, next) => {
  const update = inject(ClientUpdate);
  return next(request).pipe(
    tap({
      error: (error: unknown) => {
        const requirement = updateRequirementOf(error);
        if (requirement !== null) {
          update.require(requirement);
        }
      },
    }),
  );
};
