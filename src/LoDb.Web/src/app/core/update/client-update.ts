import { Injectable, type Signal, signal } from '@angular/core';
import type { UpdateRequirement } from './update-requirement';

/**
 * Whether the API refused this version of the app, which then shows the blocking update
 * screen until it restarts on a newer one. Fed by `updateInterceptor`; never set on the web,
 * which sends no `X-LoDb-Client` header.
 */
@Injectable({ providedIn: 'root' })
export class ClientUpdate {
  private readonly state = signal<UpdateRequirement | null>(null);

  /** The last refusal of the API; null while the version is accepted. */
  readonly requirement: Signal<UpdateRequirement | null> = this.state.asReadonly();

  /** Blocks the app: nothing it asks the API from now on would be answered. */
  require(requirement: UpdateRequirement): void {
    this.state.set(requirement);
  }
}
