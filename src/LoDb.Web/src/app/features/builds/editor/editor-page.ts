import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { provideTranslocoScope } from '@jsverse/transloco';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { MyBuilds } from '../mine/my-builds';
import type { EditorEntry } from './editor-entry';
import type { EditorView } from './editor-view';
import { BuildEditor } from './form/build-editor';
import { applyPrivateHead } from './shared/apply-private-head';
import { BUILDS_EDITOR_SCOPE } from './shared/builds-editor-scope';

/**
 * The page of the account's builds, `/{locale}/account/builds/**`, rendered in the browser
 * only: it writes the private head of its title and shows the list or the editor its route
 * names, each loaded on its own. Each entry the route resolves opens a new editor, so an
 * import to another patch starts from what that import carried.
 */
@Component({
  selector: 'lodb-editor-page',
  imports: [BuildEditor, MyBuilds],
  providers: [provideTranslocoScope(BUILDS_EDITOR_SCOPE)],
  template: `
    <div class="mx-auto max-w-5xl px-4 py-10 sm:px-6">
      @switch (view()) {
        @case ('mine') {
          @defer (on immediate) {
            <lodb-my-builds />
          }
        }
        @default {
          @for (entry of entries(); track entry) {
            @defer (on immediate) {
              <lodb-build-editor />
            }
          }
        }
      }
    </div>
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditorPage {
  protected readonly view = injectRouteData<EditorView>('view');
  private readonly entry = injectRouteData<EditorEntry | null | undefined>('entry');
  /** The resolved entry as a list of one, none on a server, which resolves nothing. */
  protected readonly entries = computed(() => {
    const entry = this.entry();
    return entry ? [entry] : [];
  });

  constructor() {
    applyPrivateHead(injectRouteData<string>('heading'));
  }
}
