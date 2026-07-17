<?php
declare(strict_types=1);

namespace App\Tests\Unit\Twig;

use App\Twig\PerformanceExtension;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;

/**
 * page_render_ms() reports the server time elapsed since the request started —
 * the inline server figure the detail-page load-time badge shows on the initial
 * (non-Turbo) load.
 */
final class PerformanceExtensionTest extends TestCase
{
    public function testReturnsElapsedMillisecondsForMainRequest(): void
    {
        $request = new Request();
        $request->server->set('REQUEST_TIME_FLOAT', microtime(true) - 0.1); // started ~100 ms ago

        $stack = new RequestStack();
        $stack->push($request);

        $ms = (new PerformanceExtension($stack))->pageRenderMs();

        self::assertGreaterThanOrEqual(0.0, $ms);
        self::assertLessThan(60000.0, $ms);
    }

    public function testReturnsZeroWithoutMainRequest(): void
    {
        self::assertSame(0.0, (new PerformanceExtension(new RequestStack()))->pageRenderMs());
    }
}
