<?php
declare(strict_types=1);

namespace App\Tests\Unit\EventSubscriber;

use App\EventSubscriber\SecurityHeadersSubscriber;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\HttpKernel\Event\ResponseEvent;
use Symfony\Component\HttpKernel\HttpKernelInterface;

/**
 * SecurityHeadersSubscriber stamps the CSP + X-Frame-Options on HTML documents
 * in prod only, so the strict `script-src` never breaks the dev profiler / Vite.
 */
final class SecurityHeadersSubscriberTest extends TestCase
{
    private function dispatch(Response $response, bool $debug, int $requestType = HttpKernelInterface::MAIN_REQUEST): Response
    {
        $event = new ResponseEvent(
            $this->createStub(HttpKernelInterface::class),
            new Request(),
            $requestType,
            $response,
        );
        (new SecurityHeadersSubscriber($debug))->onKernelResponse($event);

        return $event->getResponse();
    }

    public function testSetsPolicyOnHtmlResponseInProd(): void
    {
        $response = $this->dispatch(new Response('<html></html>', 200, ['Content-Type' => 'text/html']), debug: false);

        $csp = $response->headers->get('Content-Security-Policy');
        self::assertNotNull($csp);
        self::assertStringContainsString("default-src 'self'", $csp);
        self::assertStringContainsString("script-src 'self'", $csp);
        self::assertStringNotContainsString("script-src 'self' 'unsafe-inline'", $csp);
        self::assertStringContainsString("frame-ancestors 'none'", $csp);
        self::assertStringContainsString('https://checkout.stripe.com', $csp);
        self::assertStringContainsString('https://d28xe8vt774jo5.cloudfront.net', $csp);
        self::assertSame('DENY', $response->headers->get('X-Frame-Options'));
    }

    public function testTreatsContentTypeLessResponseAsHtml(): void
    {
        // Twig-rendered responses carry no explicit Content-Type until prepare().
        $response = $this->dispatch(new Response('<html></html>'), debug: false);

        self::assertTrue($response->headers->has('Content-Security-Policy'));
    }

    public function testSkipsInDebug(): void
    {
        $response = $this->dispatch(new Response('<html></html>', 200, ['Content-Type' => 'text/html']), debug: true);

        self::assertFalse($response->headers->has('Content-Security-Policy'));
        self::assertFalse($response->headers->has('X-Frame-Options'));
    }

    public function testSkipsNonHtmlResponse(): void
    {
        $response = $this->dispatch(new Response('{}', 200, ['Content-Type' => 'application/json']), debug: false);

        self::assertFalse($response->headers->has('Content-Security-Policy'));
    }

    public function testSkipsSubRequests(): void
    {
        $response = $this->dispatch(
            new Response('<html></html>', 200, ['Content-Type' => 'text/html']),
            debug: false,
            requestType: HttpKernelInterface::SUB_REQUEST,
        );

        self::assertFalse($response->headers->has('Content-Security-Policy'));
    }

    public function testYieldsToPreSetPolicy(): void
    {
        $scoped = "default-src 'self'; script-src 'self' 'unsafe-inline'";
        $response = $this->dispatch(
            new Response('<html></html>', 200, ['Content-Type' => 'text/html', 'Content-Security-Policy' => $scoped]),
            debug: false,
        );

        self::assertSame($scoped, $response->headers->get('Content-Security-Policy'));
        self::assertFalse($response->headers->has('X-Frame-Options'));
    }
}
