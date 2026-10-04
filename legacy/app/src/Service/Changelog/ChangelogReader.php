<?php
declare(strict_types=1);

namespace App\Service\Changelog;

use Symfony\Component\DependencyInjection\Attribute\Autowire;

/**
 * Query side of the published player-facing changelog.
 *
 * The internal technical journal (docs/changelog/) is synthesized at release
 * time into versioned artifacts under public/changelog/ — a `manifest.json`
 * index (newest-first) plus one `<id>.json` per release. This reader resolves
 * the manifest ordering to the full release payloads consumed by the page.
 *
 * It never throws: a missing or corrupt changelog degrades to an empty history
 * rather than a 500 on a cosmetic page.
 */
final class ChangelogReader
{
    /**
     * Displayed when the manifest is missing/corrupt or carries no version —
     * a degraded state that never occurs with the committed artifacts, kept
     * only so the navbar badge never renders "v" with nothing after it.
     */
    private const string UNKNOWN_VERSION = '0.0.0';

    private readonly string $dir;

    public function __construct(#[Autowire('%kernel.project_dir%')] string $projectDir)
    {
        $this->dir = $projectDir . '/public/changelog';
    }

    /**
     * The current application version: the newest manifest entry's version.
     *
     * The published changelog is the single source of truth for the release
     * number surfaced across the UI (navbar, footer, /changelog) — releasing a
     * new patch bumps the app version, with no parameter to keep in sync.
     */
    public function latestVersion(): string
    {
        $version = $this->manifest()[0]['version'] ?? null;

        return \is_string($version) && $version !== '' ? $version : self::UNKNOWN_VERSION;
    }

    /**
     * Full release payloads, ordered newest-first per the manifest. A manifest
     * entry whose `<id>.json` is missing is skipped rather than faked.
     *
     * @return list<array<string, mixed>>
     */
    public function releases(): array
    {
        $releases = [];
        foreach ($this->manifest() as $entry) {
            $id = \is_array($entry) ? ($entry['id'] ?? null) : null;
            if (!\is_string($id)) {
                continue;
            }

            $release = $this->read($id . '.json');
            if ($release !== null) {
                $releases[] = $release;
            }
        }

        return $releases;
    }

    /**
     * Manifest patch summaries, newest-first (as authored).
     *
     * @return list<array<string, mixed>>
     */
    public function manifest(): array
    {
        $patches = $this->read('manifest.json')['patches'] ?? null;

        return \is_array($patches) ? array_values($patches) : [];
    }

    /** @return array<string, mixed>|null */
    private function read(string $file): ?array
    {
        $path = $this->dir . '/' . $file;
        if (!is_file($path)) {
            return null;
        }

        $decoded = json_decode((string) file_get_contents($path), true);

        return \is_array($decoded) ? $decoded : null;
    }
}
