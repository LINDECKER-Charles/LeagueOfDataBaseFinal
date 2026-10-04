import { Injectable, inject } from '@angular/core';
import { ANDROID_PLUGINS } from '../native/android-plugins-token';
import { SecureItem } from '../native/secure-item';
import type { PendingGoogleSignIn } from './pending-google-sign-in';

const PENDING_KEY = 'lodb.google-sign-in';
// Google's authorization codes live ten minutes; a return later than that cannot succeed.
const PENDING_LIFETIME_MS = 10 * 60_000;
const TEXT_FIELDS = ['clientId', 'redirectUri', 'state', 'verifier', 'landingUrl', 'locale'];

function isPendingGoogleSignIn(value: unknown): value is PendingGoogleSignIn {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const fields = value as Record<string, unknown>;
  return (
    TEXT_FIELDS.every((field) => typeof fields[field] === 'string') &&
    typeof fields['isRemembered'] === 'boolean' &&
    typeof fields['startedAt'] === 'number'
  );
}

function parsed(stored: string): unknown {
  try {
    return JSON.parse(stored);
  } catch {
    return null;
  }
}

/** The one Google sign-in in flight: a new one replaces it, its return consumes it. */
@Injectable({ providedIn: 'root' })
export class PendingGoogleSignIns {
  private readonly item = new SecureItem(inject(ANDROID_PLUGINS).secureStorage, PENDING_KEY);

  save(flow: PendingGoogleSignIn): Promise<void> {
    return this.item.write(JSON.stringify(flow));
  }

  /** The flow in flight, erased so that its return cannot be replayed; null once expired. */
  async take(): Promise<PendingGoogleSignIn | null> {
    const stored = await this.item.read();
    if (stored === null) {
      return null;
    }
    await this.item.erase();
    const flow = parsed(stored);
    return isPendingGoogleSignIn(flow) && Date.now() - flow.startedAt < PENDING_LIFETIME_MS
      ? flow
      : null;
  }
}
