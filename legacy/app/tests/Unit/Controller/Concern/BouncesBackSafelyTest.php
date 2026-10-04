<?php
declare(strict_types=1);

namespace App\Tests\Unit\Controller\Concern;

use App\Controller\Concern\BouncesBackSafely;
use PHPUnit\Framework\TestCase;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\DependencyInjection\Container;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;

/**
 * The shared "send them back where they came from" rule of the POST-only
 * endpoints: a Referer is followed only when it shares the request's ORIGIN
 * (scheme + host), so the endpoints can never be turned into an open redirect
 * nor into an https → http downgrade.
 */
final class BouncesBackSafelyTest extends TestCase
{
    private const FALLBACK_ROUTE = 'app_trends';
    private const FALLBACK_URL = '/trends';

    public function testFollowsASameOriginReferer(): void
    {
        $response = $this->bounce(
            'https://lodb.test',
            referer: 'https://lodb.test/builds/12?page=2',
        );

        self::assertSame('https://lodb.test/builds/12?page=2', $response->getTargetUrl());
    }

    public function testFallsBackWhenTheRefererIsAnotherHost(): void
    {
        $response = $this->bounce('https://lodb.test', referer: 'https://evil.test/steal');

        self::assertSame(self::FALLBACK_URL, $response->getTargetUrl());
    }

    public function testFallsBackWhenTheRefererDowngradesTheScheme(): void
    {
        // Same host, cleartext scheme: comparing hosts alone would have accepted
        // it and bounced an https visitor down to http.
        $response = $this->bounce('https://lodb.test', referer: 'http://lodb.test/builds/12');

        self::assertSame(self::FALLBACK_URL, $response->getTargetUrl());
    }

    /**
     * The regression a prefix comparison lets through: `lodb.test.evil.test`
     * starts with `https://lodb.test`, so str_starts_with() would follow it.
     */
    public function testFallsBackWhenTheRefererHostMerelyStartsWithOurs(): void
    {
        $response = $this->bounce(
            'https://lodb.test',
            referer: 'https://lodb.test.evil.test/steal',
        );

        self::assertSame(self::FALLBACK_URL, $response->getTargetUrl());
    }

    /** Userinfo hides the real host: everything before the @ is a decoy. */
    public function testFallsBackWhenTheRefererHidesTheHostBehindUserinfo(): void
    {
        $response = $this->bounce(
            'https://lodb.test',
            referer: 'https://lodb.test@evil.test/steal',
        );

        self::assertSame(self::FALLBACK_URL, $response->getTargetUrl());
    }

    public function testFallsBackWhenTheRefererIsProtocolRelative(): void
    {
        $response = $this->bounce('https://lodb.test', referer: '//evil.test/steal');

        self::assertSame(self::FALLBACK_URL, $response->getTargetUrl());
    }

    public function testFallsBackWhenNoRefererIsSent(): void
    {
        self::assertSame(self::FALLBACK_URL, $this->bounce('https://lodb.test')->getTargetUrl());
    }

    public function testKeepsTheRequestedStatusOnBothBranches(): void
    {
        $followed = $this->bounce(
            'https://lodb.test',
            referer: 'https://lodb.test/trends',
            status: Response::HTTP_SEE_OTHER,
        );
        $fallback = $this->bounce('https://lodb.test', status: Response::HTTP_SEE_OTHER);

        self::assertSame(Response::HTTP_SEE_OTHER, $followed->getStatusCode());
        self::assertSame(Response::HTTP_SEE_OTHER, $fallback->getStatusCode());
    }

    private function bounce(
        string $origin,
        ?string $referer = null,
        int $status = Response::HTTP_FOUND,
    ): RedirectResponse {
        $request = Request::create($origin . '/builds/12/vote', 'POST');
        if ($referer !== null) {
            $request->headers->set('referer', $referer);
        }

        return $this->controller()->bounce($request, self::FALLBACK_ROUTE, $status);
    }

    private function controller(): object
    {
        $router = $this->createStub(UrlGeneratorInterface::class);
        $router->method('generate')->willReturn(self::FALLBACK_URL);

        $container = new Container();
        $container->set('router', $router);

        $controller = new class extends AbstractController {
            use BouncesBackSafely;

            public function bounce(
                Request $request,
                string $fallbackRoute,
                int $status,
            ): RedirectResponse {
                return $this->backToOrigin($request, $fallbackRoute, $status);
            }
        };
        $controller->setContainer($container);

        return $controller;
    }
}
