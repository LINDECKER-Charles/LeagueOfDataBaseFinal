<?php

declare(strict_types=1);

namespace App\Tests\Unit\EventSubscriber;

use App\EventSubscriber\RetiredDomainSubscriber;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpKernel\Event\RequestEvent;
use Symfony\Component\HttpKernel\HttpKernelInterface;
use Twig\Environment;

final class RetiredDomainSubscriberTest extends TestCase
{
    private function event(Request $request, int $type = HttpKernelInterface::MAIN_REQUEST): RequestEvent
    {
        return new RequestEvent($this->createStub(HttpKernelInterface::class), $request, $type);
    }

    public function testRendersInterstitialForRetiredHostPreservingPathAndQuery(): void
    {
        $captured = [];
        $twig = $this->createMock(Environment::class);
        $twig->expects($this->once())
            ->method('render')
            ->with(
                'system/retired_domain.html.twig',
                $this->callback(function (array $context) use (&$captured): bool {
                    $captured = $context;

                    return true;
                }),
            )
            ->willReturn('<html>notice</html>');

        $event = $this->event(Request::create('http://league-of-data-base.fr/champions/aatrox?skin=2'));
        (new RetiredDomainSubscriber($twig))->onKernelRequest($event);

        $response = $event->getResponse();
        self::assertNotNull($response);
        self::assertSame(200, $response->getStatusCode());
        self::assertSame('<html>notice</html>', $response->getContent());
        self::assertStringContainsString('no-store', (string) $response->headers->get('Cache-Control'));
        self::assertSame('noindex', $response->headers->get('X-Robots-Tag'));
        // Own scoped CSP so its trusted inline redirect script survives the
        // site-wide `script-src 'self'` (SecurityHeadersSubscriber yields to it).
        self::assertStringContainsString(
            "script-src 'self' 'unsafe-inline'",
            (string) $response->headers->get('Content-Security-Policy'),
        );
        self::assertSame(
            'https://league-of-data-base.com/champions/aatrox?skin=2',
            $captured['target_url'],
        );
    }

    public function testMapsWwwFrHostToApexComOrigin(): void
    {
        $captured = [];
        $twig = $this->createMock(Environment::class);
        $twig->method('render')
            ->with('system/retired_domain.html.twig', $this->callback(
                function (array $context) use (&$captured): bool {
                    $captured = $context;

                    return true;
                },
            ))
            ->willReturn('<html></html>');

        $event = $this->event(Request::create('http://www.league-of-data-base.fr/runes'));
        (new RetiredDomainSubscriber($twig))->onKernelRequest($event);

        self::assertNotNull($event->getResponse());
        self::assertSame('https://league-of-data-base.com/runes', $captured['target_url']);
    }

    public function testIgnoresNonRetiredHost(): void
    {
        $twig = $this->createMock(Environment::class);
        $twig->expects($this->never())->method('render');

        $event = $this->event(Request::create('http://league-of-data-base.com/champions'));
        (new RetiredDomainSubscriber($twig))->onKernelRequest($event);

        self::assertNull($event->getResponse());
    }

    public function testIgnoresSubRequests(): void
    {
        $twig = $this->createMock(Environment::class);
        $twig->expects($this->never())->method('render');

        $event = $this->event(
            Request::create('http://league-of-data-base.fr/champions'),
            HttpKernelInterface::SUB_REQUEST,
        );
        (new RetiredDomainSubscriber($twig))->onKernelRequest($event);

        self::assertNull($event->getResponse());
    }
}
