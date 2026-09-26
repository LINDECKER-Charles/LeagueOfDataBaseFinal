import { Injectable, Injector, type Type, inject } from '@angular/core';
import type { AuthStrategyKind } from '../../platform/auth-strategy-kind';
import type { AuthStrategy } from './auth-strategy';

/**
 * The registration point of the authentication strategies, by kind. `provideAuth` registers
 * `cookie`; a platform registers its own while it is built, before the detection completes:
 * the desktop platform its `host` strategy (L9.3), the Android one its `bearer` strategy
 * (L10.2), from their own folders, without touching `core/auth`:
 *
 * ```ts
 * constructor() {
 *   inject(AuthStrategies).register('host', HostAuthStrategy);
 * }
 * ```
 *
 * A class rather than an instance: it is built on first use, once `PLATFORM` and
 * `API_BASE_URL`, which a strategy may inject, can be read. It must be injectable from root.
 */
@Injectable({ providedIn: 'root' })
export class AuthStrategies {
  private readonly injector = inject(Injector);
  private readonly strategies = new Map<AuthStrategyKind, Type<AuthStrategy>>();

  /** Registers the implementation of `kind`, replacing any earlier one. */
  register(kind: AuthStrategyKind, strategy: Type<AuthStrategy>): void {
    this.strategies.set(kind, strategy);
  }

  /** The implementation of `kind`; fails loudly when no platform registered it. */
  get(kind: AuthStrategyKind): AuthStrategy {
    const strategy = this.strategies.get(kind);
    if (strategy === undefined) {
      throw new Error(`No AuthStrategy is registered for '${kind}'.`);
    }
    return this.injector.get(strategy);
  }
}
