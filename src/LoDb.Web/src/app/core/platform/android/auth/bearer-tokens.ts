import { HttpClient, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { Injectable, Injector, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../../api/api-base-url';
import { refreshAccountToken } from '../../../api/generated/fn/account/refresh-account-token';
import type { AccessTokenResponse } from '../../../api/generated/models/access-token-response';
import { AuthSession } from '../../../auth/session/auth-session';
import { ANDROID_PLUGINS } from '../native/android-plugins-token';
import { SecureItem } from '../native/secure-item';
import { BEARER_BYPASS } from './bearer-bypass';

const REFRESH_TOKEN_KEY = 'lodb.refresh-token';
const MS_PER_SECOND = 1000;
// Renewed a little before the API would refuse it, so that a request never leaves with a
// token that expires on the way.
const EXPIRY_MARGIN_MS = 30_000;
// What `/api/account/refresh` answers a refresh token it no longer honours (expired, or
// revoked by a password reset or a ban). Anything else, offline included, keeps the tokens.
const REFUSED_STATUSES = [400, 401];

interface AccessToken {
  readonly value: string;
  readonly expiresAt: number;
}

function isRefusal(error: unknown): boolean {
  return error instanceof HttpErrorResponse && REFUSED_STATUSES.includes(error.status);
}

/**
 * The tokens of the `bearer` strategy (ADR 0009, L4.2): the access token of 5 minutes only in
 * memory, the refresh token of 30 days also in the secure storage when the user asked to be
 * remembered. Renewals are shared: however many requests find the access token expired, one
 * refresh leaves, and the refresh token it rotates is stored again.
 */
@Injectable({ providedIn: 'root' })
export class BearerTokens {
  private readonly http = inject(HttpClient);
  private readonly apiOrigin = inject(API_BASE_URL);
  private readonly injector = inject(Injector);
  private readonly storedRefreshToken = new SecureItem(
    inject(ANDROID_PLUGINS).secureStorage,
    REFRESH_TOKEN_KEY,
  );
  private access: AccessToken | null = null;
  private refreshToken: string | null = null;
  private isPersistent = false;
  private restoring: Promise<void> | null = null;
  private renewal: Promise<string | null> | null = null;
  // Bumped by every sign-in and sign-out, so that a renewal started before cannot undo it.
  private generation = 0;

  /** Whether a session is held, in memory or in the secure storage of an earlier run. */
  async hasSession(): Promise<boolean> {
    await this.restore();
    return this.refreshToken !== null;
  }

  /**
   * A live access token, renewed first when it expired; null without a session, or once the
   * API refused the refresh token. Rejects when the API cannot be reached: offline is not a
   * sign-out, and the refresh token stays for the next attempt.
   */
  async accessToken(): Promise<string | null> {
    await this.restore();
    if (this.access !== null && Date.now() < this.access.expiresAt) {
      return this.access.value;
    }
    return this.renew();
  }

  /** After a 401 answered to `rejected`: renews, unless a renewal already replaced it. */
  async replace(rejected: string): Promise<string | null> {
    await this.restore();
    const current = this.access?.value;
    return current !== undefined && current !== rejected ? current : this.renew();
  }

  /** Keeps the tokens of a sign-in; the refresh token outlives the app when `isPersistent`. */
  async adopt(response: AccessTokenResponse, isPersistent: boolean): Promise<void> {
    await this.restore();
    this.generation++;
    this.isPersistent = isPersistent;
    await this.keep(response);
    if (!isPersistent) {
      await this.storedRefreshToken.erase();
    }
  }

  /** Forgets every token. The API cannot revoke them: the access token dies within minutes. */
  async clear(): Promise<void> {
    await this.restore();
    this.generation++;
    this.access = null;
    this.refreshToken = null;
    await this.storedRefreshToken.erase();
  }

  private restore(): Promise<void> {
    this.restoring ??= this.storedRefreshToken.read().then((token) => {
      this.refreshToken = token;
      this.isPersistent = token !== null;
    });
    return this.restoring;
  }

  private renew(): Promise<string | null> {
    this.renewal ??= this.redeem().finally(() => (this.renewal = null));
    return this.renewal;
  }

  private async redeem(): Promise<string | null> {
    const { generation, refreshToken } = this;
    if (refreshToken === null) {
      return null;
    }
    try {
      const { body } = await firstValueFrom(
        refreshAccountToken(
          this.http,
          this.apiOrigin,
          { body: { refreshToken } },
          new HttpContext().set(BEARER_BYPASS, true),
        ),
      );
      if (generation === this.generation) {
        await this.keep(body);
      }
    } catch (error) {
      if (!isRefusal(error)) {
        throw error;
      }
      if (generation === this.generation) {
        await this.expire();
      }
    }
    return this.access?.value ?? null;
  }

  private async keep(response: AccessTokenResponse): Promise<void> {
    const lifetime = response.expiresIn * MS_PER_SECOND - EXPIRY_MARGIN_MS;
    this.access = { value: response.accessToken, expiresAt: Date.now() + lifetime };
    this.refreshToken = response.refreshToken;
    if (this.isPersistent) {
      await this.storedRefreshToken.write(response.refreshToken);
    }
  }

  // The session ended on the API's side: every page learns it is signed out.
  private async expire(): Promise<void> {
    await this.clear();
    this.injector.get(AuthSession).apply({ user: null });
  }
}
