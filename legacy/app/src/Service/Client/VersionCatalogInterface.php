<?php

declare(strict_types=1);

namespace App\Service\Client;

/**
 * Read-only view over the Data Dragon patch/language catalog.
 *
 * Consumers that only need to *ask* the catalog a question (is this patch valid?
 * what is the newest one?) depend on this contract rather than on
 * {@see VersionManager}, which additionally owns upstream fetching, caching and
 * user-facing labels — three reasons to change that its callers should not inherit.
 */
interface VersionCatalogInterface
{
    /**
     * @return string[] every known Data Dragon patch, newest first
     */
    public function getVersions(): array;

    /** Newest known patch, or '' when the catalog is unavailable. */
    public function latestVersion(): string;

    public function versionExists(?string $version): bool;

    public function languageExists(?string $language): bool;
}
