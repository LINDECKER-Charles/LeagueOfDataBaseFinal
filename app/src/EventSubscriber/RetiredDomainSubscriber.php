<?php

declare(strict_types=1);

namespace App\EventSubscriber;

use Symfony\Component\EventDispatcher\EventSubscriberInterface;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\HttpKernel\Event\RequestEvent;
use Symfony\Component\HttpKernel\KernelEvents;
use Twig\Environment;

/**
 * Retires the legacy French domain. Every hit on a `.fr` host is answered with a
 * branded notice that self-redirects to the same path on the canonical `.com`
 * origin, instead of a silent 301, so residual visitors learn the address moved
 * before they land on the new site.
 *
 * The destination is a constant origin and the trigger is an explicit host
 * allowlist — never a blind `.fr` -> `.com` rewrite of the Host header. The Host
 * is client-controlled, so deriving the target from it would be an open-redirect
 * vector; we only ever send traffic to our own site.
 *
 * Runs ahead of the router (priority 32) and {@see LocaleSubscriber} (20): a
 * retired domain has no reason to pay for routing, session or locale resolution
 * just to be redirected away.
 */
final class RetiredDomainSubscriber implements EventSubscriberInterface
{
    private const RETIRED_HOSTS = ['league-of-data-base.fr', 'www.league-of-data-base.fr'];
    private const TARGET_ORIGIN = 'https://league-of-data-base.com';
    private const REDIRECT_DELAY_SECONDS = 5;

    public function __construct(private readonly Environment $twig) {}

    public static function getSubscribedEvents(): array
    {
        return [KernelEvents::REQUEST => [['onKernelRequest', 40]]];
    }

    public function onKernelRequest(RequestEvent $event): void
    {
        if (!$event->isMainRequest()) {
            return;
        }

        $request = $event->getRequest();
        if (!in_array($request->getHost(), self::RETIRED_HOSTS, true)) {
            return;
        }

        // Same path + query on the .com origin — getRequestUri() is already the
        // raw, encoded URI as received; Twig attribute-escapes it on output.
        $html = $this->twig->render('system/retired_domain.html.twig', [
            'target_url' => self::TARGET_ORIGIN . $request->getRequestUri(),
            'redirect_delay' => self::REDIRECT_DELAY_SECONDS,
        ]);

        // 200, not 301: the visitor must actually see the notice. `no-store` stops
        // the interstitial being cached in place of real content, and X-Robots-Tag
        // keeps the dead host out of search indexes.
        //
        // Own scoped CSP: this standalone page carries a trusted inline redirect
        // script (the target comes from a data-attribute, never inlined into JS).
        // SecurityHeadersSubscriber yields to a response that already declares a
        // CSP, so the interstitial keeps 'unsafe-inline' here without loosening
        // the site-wide `script-src 'self'`.
        $event->setResponse(new Response($html, Response::HTTP_OK, [
            'Cache-Control' => 'no-store',
            'X-Robots-Tag' => 'noindex',
            'X-Frame-Options' => 'DENY',
            'Content-Security-Policy' => "default-src 'self'; script-src 'self' 'unsafe-inline'; "
                . "style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; "
                . "base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'",
        ]));
    }
}
