import type { TestRequest } from '@angular/common/http/testing';

/** What the API answers a call: a JSON body, text, or nothing. */
export type Answer = Parameters<TestRequest['flush']>[0];
