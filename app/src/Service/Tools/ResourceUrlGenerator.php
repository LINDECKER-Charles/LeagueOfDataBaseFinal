<?php

declare(strict_types=1);

namespace App\Service\Tools;

use App\Service\Client\VersionManager;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;

/**
 * Builds resource URLs that keep the latest patch on the clean, canonical route
 * and pin a historical patch in the path (`/{version}/champion/…`). This is the
 * single place that decides "clean vs versioned": internal links must point at
 * the canonical URL of the content they reference — the clean URL for the latest
 * patch, the versioned URL for any older one, so navigation stays on its patch.
 *
 * The Data Dragon data language stays a query param (`?lang=`): it selects the
 * render language of a shareable URL and is distinct from the UI locale — it is
 * dropped from the canonical, so it never forks the index.
 */
final class ResourceUrlGenerator
{
    /** Suffix of the `/{version}/…` route paired with each clean resource route. */
    private const VERSIONED_SUFFIX = '_versioned';

    public function __construct(
        private readonly UrlGeneratorInterface $router,
        private readonly VersionManager $versionManager,
    ) {}

    /**
     * @param string               $route  clean resource route name (e.g. "app_champion")
     * @param array<string,scalar> $params clean-route params (e.g. {name: "Aatrox"})
     */
    public function resourcePath(string $route, array $params = [], string $version = '', string $lang = ''): string
    {
        $version = trim($version);
        $latest  = $this->versionManager->getVersions()[0] ?? '';

        $url = $version !== '' && $version !== $latest
            ? $this->router->generate($route . self::VERSIONED_SUFFIX, ['version' => $version] + $params)
            : $this->router->generate($route, $params);

        $lang = trim($lang);
        if ($lang !== '') {
            $url .= (str_contains($url, '?') ? '&' : '?') . 'lang=' . rawurlencode($lang);
        }

        return $url;
    }
}
