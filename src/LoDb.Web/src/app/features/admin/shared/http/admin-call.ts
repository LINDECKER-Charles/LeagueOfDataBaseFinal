import type { HttpClient } from '@angular/common/http';
import type { Observable } from 'rxjs';
import type { StrictHttpResponse } from '../../../../core/api/generated/strict-http-response';

/**
 * An operation of the generated client, as `ng-openapi-gen` writes them; their optional
 * `HttpContext`, which the admin never passes, is left out.
 */
export type AdminCall<P, T> = (
  http: HttpClient,
  rootUrl: string,
  params: P,
) => Observable<StrictHttpResponse<T>>;
