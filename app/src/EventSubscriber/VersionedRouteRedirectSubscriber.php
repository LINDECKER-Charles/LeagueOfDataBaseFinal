<?php

declare(strict_types=1);

namespace App\EventSubscriber;

use App\Service\Client\VersionManager;
use Symfony\Component\EventDispatcher\EventSubscriberInterface;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\HttpKernel\Event\RequestEvent;
use Symfony\Component\HttpKernel\KernelEvents;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;

/**
 * Keeps the latest patch on the clean, canonical URL. A `/{version}/…` request
 * whose version is the current latest is a duplicate of the query-less page, so
 * it is 301'd to the clean route; a historical version is served as-is and
 * self-canonicalizes (it is an independently indexable snapshot of that patch).
 *
 * Runs at priority 30 — after Symfony's RouterListener (32) so `_route` and
 * `_route_params` are populated, before the firewall so the redirect is cheap.
 */
final class VersionedRouteRedirectSubscriber implements EventSubscriberInterface
{
    /** Suffix marking the `/{version}/…` variant of a resource route. */
    private const VERSIONED_SUFFIX = '_versioned';

    public function __construct(
        private readonly VersionManager $versionManager,
        private readonly UrlGeneratorInterface $router,
    ) {}

    public static function getSubscribedEvents(): array
    {
        return [KernelEvents::REQUEST => [['onKernelRequest', 30]]];
    }

    public function onKernelRequest(RequestEvent $event): void
    {
        if (!$event->isMainRequest()) {
            return;
        }

        $request = $event->getRequest();
        $route   = (string) $request->attributes->get('_route');
        if (!str_ends_with($route, self::VERSIONED_SUFFIX)) {
            return;
        }

        $version = (string) $request->attributes->get('version', '');
        $latest  = $this->versionManager->getVersions()[0] ?? null;
        if ($version === '' || $version !== $latest) {
            return; // historical snapshot → serve, self-canonical
        }

        $event->setResponse(new RedirectResponse($this->cleanUrl($request, $route), Response::HTTP_MOVED_PERMANENTLY));
    }

    /** Clean-route URL for the current versioned request, query string preserved. */
    private function cleanUrl(Request $request, string $route): string
    {
        $params = (array) $request->attributes->get('_route_params', []);
        unset($params['version']);

        $cleanRoute = substr($route, 0, -\strlen(self::VERSIONED_SUFFIX));
        $url        = $this->router->generate($cleanRoute, $params);

        $query = $request->getQueryString();

        return $query === null || $query === '' ? $url : $url . '?' . $query;
    }
}
