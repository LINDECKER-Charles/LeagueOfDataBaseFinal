import { HttpContext } from '@angular/common/http';
import { BEARER_BYPASS } from './bearer-bypass';

/** The context of a token call, which the `bearer` strategy sends without a token. */
export function bypassContext(): HttpContext {
  return new HttpContext().set(BEARER_BYPASS, true);
}
