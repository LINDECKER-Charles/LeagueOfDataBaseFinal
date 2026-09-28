import type { ChangelogRelease } from '../model/changelog-release';
import type { ReleaseFeature } from '../model/release-feature';
import type { ReleaseFix } from '../model/release-fix';
import type { ReleaseType } from '../model/release-type';
import { isJsonRecord } from './is-json-record';
import { jsonRecords } from './json-records';
import { jsonText } from './json-text';

const TYPES: readonly ReleaseType[] = ['major', 'minor', 'hotfix'];
const DEFAULT_GLYPH = '◆';

function releaseType(value: unknown): ReleaseType {
  return TYPES.find((type) => type === value) ?? 'minor';
}

function features(value: unknown): readonly ReleaseFeature[] {
  return jsonRecords(value).flatMap((feature) => {
    const title = jsonText(feature['title']);
    if (title === null) {
      return [];
    }
    return [
      {
        kind: feature['kind'] === 'mana' ? 'mana' : 'feature',
        glyph: jsonText(feature['glyph']) ?? DEFAULT_GLYPH,
        tag: jsonText(feature['tag']),
        title,
        description: jsonText(feature['description']) ?? '',
      },
    ];
  });
}

function fixes(value: unknown): readonly ReleaseFix[] {
  return jsonRecords(value).flatMap((fix) => {
    const text = jsonText(fix['text']);
    return text === null ? [] : [{ id: jsonText(fix['id']), area: jsonText(fix['area']), text }];
  });
}

function devNote(value: unknown): ChangelogRelease['devNote'] {
  if (!isJsonRecord(value)) {
    return null;
  }
  const body = jsonText(value['body']);
  return body === null ? null : { body, signature: jsonText(value['signature']) };
}

/**
 * A release file, or `null` when it is not one (no version to show). Every other field is
 * optional: what is missing is left out of the page rather than failing it. The `balances`
 * block of the files is not shown, as on the current site.
 */
export function parseRelease(json: unknown): ChangelogRelease | null {
  if (!isJsonRecord(json)) {
    return null;
  }
  const version = jsonText(json['version']);
  if (version === null) {
    return null;
  }
  return {
    version,
    codename: jsonText(json['codename']) ?? '',
    date: jsonText(json['date']) ?? '',
    type: releaseType(json['type']),
    summary: jsonText(json['summary']) ?? '',
    intro: jsonText(json['intro']),
    features: features(json['features']),
    fixes: fixes(json['bugfixes']),
    devNote: devNote(json['devNote']),
  };
}
