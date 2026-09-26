import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { BuildStructure } from '../../../../core/api/generated/models/build-structure';
import type { CatalogMeta } from '../../../../core/api/generated/models/catalog-meta';
import { BuildsService } from '../../../../core/api/generated/services/builds.service';
import { ApiMeta } from '../../../../core/api/meta/api-meta';
import { pageContextOf } from '../../../../core/context/page-context-of';
import { PreferencesStore } from '../../../../core/context/preferences/preferences-store';
import type { EditorEntry } from '../editor-entry';
import { draftOf } from './draft-of';
import type { EntryRequest } from './entry-request';
import { queryOf } from './query-of';

/** What the editor opens on, before the lists of the context are added. */
type Loaded = Pick<EditorEntry, 'mode' | 'buildId' | 'draft' | 'report'>;

/** The catalogue a request reads: the patch and the language of its URL, or the defaults. */
interface EntryContext {
  readonly meta: CatalogMeta;
  readonly version: string;
  readonly lang: string;
}

const BLANK_STRUCTURE: BuildStructure = {
  championId: '',
  runes: { primaryStyleId: 0, primarySelections: [], secondaryStyleId: 0, secondarySelections: [] },
  steps: [],
};

function blankOf({ meta, version, lang }: EntryContext): Loaded {
  const draft = {
    name: '',
    description: null,
    isPublic: false,
    gameVersion: version,
    gameMode: meta.defaultGameMode,
    language: lang,
    structure: BLANK_STRUCTURE,
  };
  return { mode: 'create', buildId: null, draft, report: null };
}

/**
 * Loads what an editor opens on: a blank build on the patch browsed, an owned build as
 * saved, or an owned build carried over to another patch (the latest when the URL names
 * none it knows), with what the carrying changed. Failures are the resolver's to answer.
 */
@Injectable({ providedIn: 'root' })
export class EditorEntries {
  private readonly meta = inject(ApiMeta);
  private readonly builds = inject(BuildsService);
  private readonly preferences = inject(PreferencesStore);

  async load(request: EntryRequest): Promise<EditorEntry> {
    const context = await this.contextOf(request);
    const loaded = await this.loaded(request, context);
    return {
      ...loaded,
      lang: context.lang,
      versions: context.meta.versions,
      languages: context.meta.languages,
      gameModes: context.meta.gameModes.map((choice) => choice.mode),
    };
  }

  private async contextOf(request: EntryRequest): Promise<EntryContext> {
    const meta = await firstValueFrom(this.meta.meta());
    const sources = {
      locale: request.locale,
      path: null,
      query: queryOf(request.url),
      remembered: this.preferences.read(),
    };
    const context = pageContextOf(sources, meta);
    return {
      meta,
      version: context?.version ?? meta.latest ?? '',
      lang: context?.language ?? meta.defaultLanguage,
    };
  }

  private async loaded(request: EntryRequest, context: EntryContext): Promise<Loaded> {
    const id = request.id ?? 0;
    if (request.view === 'edit') {
      const build = await firstValueFrom(this.builds.getBuild({ id }));
      return { mode: 'edit', buildId: id, draft: draftOf(build), report: null };
    }
    if (request.view === 'import') {
      const known = request.to !== null && context.meta.versions.includes(request.to);
      const to = known ? (request.to ?? undefined) : undefined;
      const preview = await firstValueFrom(
        this.builds.previewBuildImport({ id, lang: context.lang, to }),
      );
      return { mode: 'create', buildId: id, draft: preview.draft, report: preview.report };
    }
    return blankOf(context);
  }
}
