<?php
declare(strict_types=1);

namespace App\EventSubscriber;

use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Component\EventDispatcher\EventSubscriberInterface;
use Symfony\Component\HttpKernel\Event\ResponseEvent;
use Symfony\Component\HttpKernel\KernelEvents;

/**
 * Emits the environment-sensitive document headers — Content-Security-Policy and
 * the X-Frame-Options clickjacking guard — on HTML responses in production.
 *
 * These two live here rather than in nginx (which carries the constant, dev-safe
 * headers, see docker/nginx/snippets/security-headers.conf) precisely because
 * they are env-sensitive: a strict `script-src` would break the Symfony profiler
 * toolbar and the Vite HMR client in dev. Gating on kernel.debug keeps the dev
 * tooling working while every rendered page still gets the full policy in prod.
 *
 * Scope: `text/html` (or content-type-less) main-request responses. JSON/API
 * payloads, the SSE stream and binary assets render no document and gain nothing
 * from a CSP, so they are skipped. A response that already declares its own CSP
 * (the retired-domain interstitial, which needs a scoped inline-script allowance)
 * is left untouched.
 */
final class SecurityHeadersSubscriber implements EventSubscriberInterface
{
    private const CSP_HEADER = 'Content-Security-Policy';

    /**
     * CSP Level-2, host-based. `script-src 'self'` carries no 'unsafe-inline':
     * every executable script is a same-origin Vite module from /build, JSON-LD
     * blocks are inert data, and the former inline handlers now run from the
     * bundle. `style-src` keeps 'unsafe-inline' — Vue `:style` bindings and
     * Turbo's progress bar set inline styles that a nonce/hash cannot cover.
     * The external img/media hosts are the hotlinked Riot CDNs (splash art,
     * chroma previews, ability videos). `form-action` allows the Stripe Checkout
     * 303 target; Google OAuth login is a plain link (a top-level navigation,
     * not governed here). `frame-ancestors 'none'` is the modern clickjacking
     * guard, doubled by the X-Frame-Options header below for legacy agents.
     */
    private const POLICY = "default-src 'self'; "
        . "base-uri 'self'; "
        . "object-src 'none'; "
        . "frame-src 'none'; "
        . "frame-ancestors 'none'; "
        . "form-action 'self' https://checkout.stripe.com https://billing.stripe.com; "
        . "script-src 'self'; "
        . "style-src 'self' 'unsafe-inline'; "
        . "img-src 'self' data: https://ddragon.leagueoflegends.com https://raw.communitydragon.org https://d28xe8vt774jo5.cloudfront.net; "
        . "media-src 'self' https://d28xe8vt774jo5.cloudfront.net; "
        . "font-src 'self'; "
        . "connect-src 'self'; "
        . "worker-src 'self'; "
        . "manifest-src 'self'";

    public function __construct(
        #[Autowire('%kernel.debug%')]
        private readonly bool $debug,
    ) {}

    public static function getSubscribedEvents(): array
    {
        return [KernelEvents::RESPONSE => 'onKernelResponse'];
    }

    public function onKernelResponse(ResponseEvent $event): void
    {
        if ($this->debug || !$event->isMainRequest()) {
            return;
        }

        $response = $event->getResponse();
        if ($response->headers->has(self::CSP_HEADER)) {
            return;
        }

        // Content-Type is unset on the response until Response::prepare(); treat a
        // missing/empty type as HTML (Symfony's default) and only bail on a type
        // that is explicitly non-HTML.
        $contentType = $response->headers->get('Content-Type');
        if (null !== $contentType && '' !== $contentType && !str_contains($contentType, 'text/html')) {
            return;
        }

        $response->headers->set(self::CSP_HEADER, self::POLICY);
        $response->headers->set('X-Frame-Options', 'DENY');
    }
}
