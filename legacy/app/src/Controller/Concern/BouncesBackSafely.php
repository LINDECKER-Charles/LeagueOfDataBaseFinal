<?php
declare(strict_types=1);

namespace App\Controller\Concern;

use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;

/**
 * "Send the visitor back where they came from" for the POST-only endpoints that
 * render no view of their own (contact form, verification resend, build vote).
 *
 * Single home of the anti-open-redirect rule: the Referer is trusted only when
 * its parsed scheme, host and port all equal this request's own. Comparing the
 * host alone would let an `http://` Referer bounce an `https://` page down to
 * cleartext, and a bare "starts with /" test would accept protocol-relative
 * `//evil.tld`.
 *
 * The comparison MUST parse, never prefix-match: `https://example.com` is a
 * prefix of both `https://example.com.evil.tld/` and `https://example.com@evil.tld/`,
 * either of which would turn this helper into the open redirect it exists to prevent.
 *
 * For controllers extending {@see \Symfony\Bundle\FrameworkBundle\Controller\AbstractController}.
 */
trait BouncesBackSafely
{
    /** The originating page when it is provably same-origin, else $fallbackRoute. */
    private function backToOrigin(
        Request $request,
        string $fallbackRoute,
        int $status = Response::HTTP_FOUND,
    ): RedirectResponse {
        $referer = $this->sameOriginReferer($request);

        return $referer === null
            ? $this->redirectToRoute($fallbackRoute, status: $status)
            : $this->redirect($referer, $status);
    }

    /** The request Referer, or null when it does not share this request's origin. */
    private function sameOriginReferer(Request $request): ?string
    {
        $referer = trim((string) $request->headers->get('referer', ''));
        if ($referer === '') {
            return null;
        }

        $parts = parse_url($referer);
        if ($parts === false || !isset($parts['scheme'], $parts['host'])) {
            return null;
        }
        // A userinfo section means the visible prefix is not the real host
        // (`https://site.tld@evil.tld/`): refuse rather than try to interpret it.
        if (isset($parts['user']) || isset($parts['pass'])) {
            return null;
        }

        $sameOrigin = strcasecmp($parts['scheme'], $request->getScheme()) === 0
            && strcasecmp($parts['host'], $request->getHost()) === 0
            && ($parts['port'] ?? $request->getPort()) === $request->getPort();

        return $sameOrigin ? $referer : null;
    }
}
