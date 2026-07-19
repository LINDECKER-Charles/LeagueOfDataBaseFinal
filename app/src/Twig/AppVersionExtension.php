<?php
declare(strict_types=1);

namespace App\Twig;

use App\Service\Changelog\ChangelogReader;
use Twig\Extension\AbstractExtension;
use Twig\Extension\GlobalsInterface;

/**
 * Exposes the current application version as the Twig global `app_version`
 * (navbar badge, footer, /changelog), derived from the newest published
 * changelog entry rather than a hand-maintained parameter — the changelog is
 * the single source of truth, so cutting a release bumps the version everywhere.
 *
 * The value is read once per request (globals are resolved once per Twig
 * Environment) and memoized here as a cheap safeguard against repeated reads.
 */
final class AppVersionExtension extends AbstractExtension implements GlobalsInterface
{
    private ?string $version = null;

    public function __construct(private readonly ChangelogReader $changelog) {}

    public function getGlobals(): array
    {
        return ['app_version' => $this->version ??= $this->changelog->latestVersion()];
    }
}
