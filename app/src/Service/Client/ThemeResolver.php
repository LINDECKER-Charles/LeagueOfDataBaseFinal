<?php

declare(strict_types=1);

namespace App\Service\Client;

use Symfony\Component\HttpFoundation\RequestStack;

/**
 * Resolves the {@see Theme} the current request renders with, from the
 * `lod_theme` cookie.
 *
 * Exposed to Twig as the `app_theme` global (config/packages/twig.yaml) rather
 * than through a Twig extension: the TwigBundle error pages extend
 * base.html.twig but render OUTSIDE any controller, with no `client` view-model
 * to hang a theme on — a global is the only vehicle that reaches them. It also
 * keeps src/Twig/ at its file budget.
 *
 * Resolving server-side is what removes the flash of the wrong identity: the
 * attribute ships on <html> in the first byte instead of being painted later by
 * a deferred module — which the CSP forbids anyway (`script-src 'self'`, no
 * inline, no nonce).
 */
final class ThemeResolver
{
    /** Per-request memo: the header, <html> and the chrome meta all read it. */
    private ?Theme $current = null;

    public function __construct(private readonly RequestStack $requestStack) {}

    public function current(): Theme
    {
        return $this->current ??= Theme::fromCookie(
            $this->requestStack->getCurrentRequest()?->cookies->get(Theme::COOKIE),
        );
    }

    /**
     * Every identity the toggle offers, in display order. Exists so the header
     * template iterates a view-model instead of naming the enum's FQCN.
     *
     * @return list<Theme>
     */
    public function options(): array
    {
        return Theme::cases();
    }
}
