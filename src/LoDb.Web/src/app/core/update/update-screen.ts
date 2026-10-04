import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Renderer2,
  afterRenderEffect,
  computed,
  inject,
  viewChild,
} from '@angular/core';
import { TranslocoPipe, provideTranslocoScope } from '@jsverse/transloco';
import { Button } from '../../ui/controls/button';
import { Frame } from '../../ui/surfaces/frame';
import { PLATFORM } from '../platform/platform';
import { ClientUpdate } from './client-update';
import { UpdateRestart } from './update-restart';

const INERT = 'inert';

/**
 * Blocking update screen of the apps (ADR 0008), drawn over the whole application once the
 * API refuses their version with a 426: the rest of the page turns inert, and the screen
 * follows `PlatformService.updateState` until the update is ready to restart on (Velopack on
 * desktop, live update or Play on Android). Never shown on the web. Its texts
 * (`public/i18n/update/`) load only when it shows.
 */
@Component({
  selector: 'lodb-update-screen',
  imports: [Button, Frame, TranslocoPipe],
  providers: [provideTranslocoScope('update'), UpdateRestart],
  templateUrl: './update-screen.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UpdateScreen {
  private readonly platform = inject(PLATFORM, { optional: true });
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly renderer = inject(Renderer2);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');
  protected readonly requirement = inject(ClientUpdate).requirement;
  protected readonly state = computed(() => this.platform?.updateState() ?? 'none');
  protected readonly restart = inject(UpdateRestart);

  constructor() {
    // The app stays blocked until it restarts: nothing ever lifts the inert state.
    afterRenderEffect(() => {
      const heading = this.heading()?.nativeElement;
      if (heading !== undefined) {
        this.makeSiblingsInert();
        heading.focus();
      }
    });
  }

  private makeSiblingsInert(): void {
    const host = this.host.nativeElement;
    for (const sibling of Array.from(host.parentElement?.children ?? [])) {
      if (sibling !== host) {
        this.renderer.setAttribute(sibling, INERT, '');
      }
    }
  }
}
