<?php
declare(strict_types=1);

namespace App\Tests\Unit\EventSubscriber;

use App\EventSubscriber\ResponseTimeSubscriber;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\HttpKernel\Event\ResponseEvent;
use Symfony\Component\HttpKernel\HttpKernelInterface;

/**
 * ResponseTimeSubscriber stamps X-Runtime (server ms) on main-request responses,
 * the canonical server figure the detail-page load-time badge reads off the Turbo
 * fetch response.
 */
final class ResponseTimeSubscriberTest extends TestCase
{
    private function dispatch(Request $request, int $requestType): Response
    {
        $event = new ResponseEvent(
            $this->createStub(HttpKernelInterface::class),
            $request,
            $requestType,
            new Response(),
        );
        (new ResponseTimeSubscriber())->onKernelResponse($event);

        return $event->getResponse();
    }

    public function testStampsRuntimeHeaderOnMainRequest(): void
    {
        $request = new Request();
        $request->server->set('REQUEST_TIME_FLOAT', microtime(true) - 0.05); // started ~50 ms ago

        $header = $this->dispatch($request, HttpKernelInterface::MAIN_REQUEST)->headers->get('X-Runtime');

        self::assertNotNull($header);
        self::assertGreaterThanOrEqual(0.0, (float) $header);
        self::assertLessThan(60000.0, (float) $header);
    }

    public function testIgnoresSubRequests(): void
    {
        $request = new Request();
        $request->server->set('REQUEST_TIME_FLOAT', microtime(true));

        self::assertFalse($this->dispatch($request, HttpKernelInterface::SUB_REQUEST)->headers->has('X-Runtime'));
    }

    public function testSkipsWhenRequestTimeIsMissing(): void
    {
        // A hand-built Request carries no REQUEST_TIME_FLOAT (unlike createFromGlobals).
        self::assertFalse($this->dispatch(new Request(), HttpKernelInterface::MAIN_REQUEST)->headers->has('X-Runtime'));
    }
}
