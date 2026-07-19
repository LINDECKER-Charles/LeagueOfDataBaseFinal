<?php
declare(strict_types=1);

namespace App\Tests\Unit\Controller\Concern;

use App\Controller\Concern\ThrottlesByIp;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\RateLimiter\RateLimiterFactory;
use Symfony\Component\RateLimiter\RateLimiterFactoryInterface;
use Symfony\Component\RateLimiter\Storage\InMemoryStorage;

/**
 * The shared per-IP throttle: allows up to the limit, rejects beyond it, and
 * keys strictly on the client IP so distinct hosts keep independent budgets.
 */
final class ThrottlesByIpTest extends TestCase
{
    private const LIMIT = 3;

    public function testRejectsOnlyOnceThePerIpBudgetIsExhausted(): void
    {
        $subject = $this->subject();
        $limiter = $this->limiter();
        $request = $this->requestFrom('203.0.113.7');

        for ($i = 1; $i <= self::LIMIT; $i++) {
            self::assertFalse($subject->throttled($limiter, $request), "attempt {$i} within budget");
        }
        self::assertTrue($subject->throttled($limiter, $request), 'attempt beyond budget is rejected');
    }

    public function testBudgetsAreIndependentPerIp(): void
    {
        $subject = $this->subject();
        $limiter = $this->limiter();

        // Drain host A entirely.
        $hostA = $this->requestFrom('203.0.113.7');
        for ($i = 0; $i < self::LIMIT; $i++) {
            $subject->throttled($limiter, $hostA);
        }
        self::assertTrue($subject->throttled($limiter, $hostA));

        // A different host is unaffected.
        self::assertFalse($subject->throttled($limiter, $this->requestFrom('198.51.100.4')));
    }

    private function limiter(): RateLimiterFactoryInterface
    {
        return new RateLimiterFactory(
            ['id' => 'test', 'policy' => 'fixed_window', 'limit' => self::LIMIT, 'interval' => '1 minute'],
            new InMemoryStorage(),
        );
    }

    private function requestFrom(string $ip): Request
    {
        return Request::create('/contact', 'POST', server: ['REMOTE_ADDR' => $ip]);
    }

    /** Minimal host exposing the private trait method for assertion. */
    private function subject(): object
    {
        return new class {
            use ThrottlesByIp;

            public function throttled(RateLimiterFactoryInterface $limiter, Request $request): bool
            {
                return $this->isRateLimited($limiter, $request);
            }
        };
    }
}
