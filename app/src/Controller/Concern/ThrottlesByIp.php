<?php
declare(strict_types=1);

namespace App\Controller\Concern;

use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\RateLimiter\RateLimiterFactoryInterface;

/**
 * Per-IP throttling for abuse-prone public actions (account creation, email
 * sending, payment-session creation). Each named limiter is its own bucket, so
 * hitting one endpoint never spends another's budget; the client IP is the key.
 *
 * Anonymous surfaces only — authenticated mutations must key on the account, not
 * the IP, to avoid false positives behind shared NAT / carrier-grade addresses.
 */
trait ThrottlesByIp
{
    private function isRateLimited(RateLimiterFactoryInterface $limiter, Request $request): bool
    {
        return !$limiter->create($request->getClientIp() ?? 'anon')->consume()->isAccepted();
    }
}
