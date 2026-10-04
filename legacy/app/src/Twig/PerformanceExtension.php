<?php
declare(strict_types=1);

namespace App\Twig;

use Symfony\Component\HttpFoundation\RequestStack;
use Twig\Extension\AbstractExtension;
use Twig\TwigFunction;

/**
 * Exposes the server time elapsed since the request started (ms), for the
 * detail-page load-time badge.
 *
 * Read at the point the function is called in the template (end of the detail
 * body, via {@see components/detail_actions.html.twig}), so it captures the
 * controller + Twig work done up to that render — a fair "generation time".
 * The canonical, complete figure is the X-Runtime response header
 * ({@see \App\EventSubscriber\ResponseTimeSubscriber}); this inline value is what
 * the badge shows on the initial (non-Turbo) load, where the header is not
 * reachable from JavaScript.
 */
final class PerformanceExtension extends AbstractExtension
{
    public function __construct(private readonly RequestStack $requestStack) {}

    public function getFunctions(): array
    {
        return [
            new TwigFunction('page_render_ms', $this->pageRenderMs(...)),
        ];
    }

    /** Milliseconds elapsed since the main request started (0.0 when unavailable). */
    public function pageRenderMs(): float
    {
        $start = $this->requestStack->getMainRequest()?->server->get('REQUEST_TIME_FLOAT');
        if (!is_float($start) && !is_int($start)) {
            return 0.0;
        }

        return round((microtime(true) - (float) $start) * 1000, 1);
    }
}
