// The (version, language) pairs a run compares.
import { defaultLanguages, defaultLatest, trapVersions, versionsUrl } from './settings.mjs';

/**
 * The latest `latest` versions of the upstream list, then the trap versions not already in
 * it, in that order. An explicit list replaces both.
 */
export function sampleVersions(upstream, { latest = defaultLatest, explicit } = {}) {
  if (explicit?.length) {
    return [...new Set(explicit)];
  }
  const recent = upstream.filter((version) => /^\d+\.\d+\.\d+$/.test(version)).slice(0, latest);
  return [...new Set([...recent, ...trapVersions])];
}

export function sampleLanguages(explicit) {
  return explicit?.length ? [...new Set(explicit)] : [...defaultLanguages];
}

export async function upstreamVersions() {
  const response = await fetch(versionsUrl);
  if (!response.ok) {
    throw new Error(`${versionsUrl} answered ${response.status}`);
  }
  return response.json();
}

/** Every (version, language) pair of the sample, versions first. */
export function pairs(sample) {
  return sample.versions.flatMap((version) =>
    sample.languages.map((language) => ({ version, language })));
}
