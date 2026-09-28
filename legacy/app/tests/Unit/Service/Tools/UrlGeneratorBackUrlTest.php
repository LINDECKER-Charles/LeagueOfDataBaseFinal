<?php

declare(strict_types=1);

namespace App\Tests\Unit\Service\Tools;

use App\Service\Tools\UrlGenerator;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;

/**
 * generateBackUrl() feeds a redirect, so the Referer it trusts is attacker-
 * controlled input: anything that does not start with our own origin must fall
 * back to a route. The guard is unconditional — there is no caller-side opt-out.
 */
final class UrlGeneratorBackUrlTest extends TestCase
{
    private const HOME = '/';

    public function testReturnsASameHostReferer(): void
    {
        $url = $this->generator('https://example.com/champions')->generateBackUrl();

        self::assertSame('https://example.com/champions', $url);
    }

    public function testFallsBackOnACrossHostReferer(): void
    {
        $url = $this->generator('https://evil.example.net/phish')->generateBackUrl();

        self::assertSame(self::HOME, $url);
    }

    public function testFallsBackOnASchemeMismatch(): void
    {
        // http:// on an https:// host is a different origin, not the same site.
        $url = $this->generator('http://example.com/champions')->generateBackUrl();

        self::assertSame(self::HOME, $url);
    }

    public function testFallsBackWithoutAReferer(): void
    {
        self::assertSame(self::HOME, $this->generator(null)->generateBackUrl());
    }

    private function generator(?string $referer): UrlGenerator
    {
        $request = Request::create('https://example.com/setup-submit', 'POST');
        if ($referer !== null) {
            $request->headers->set('referer', $referer);
        }

        $stack = new RequestStack();
        $stack->push($request);

        $router = $this->createStub(UrlGeneratorInterface::class);
        $router->method('generate')->willReturn(self::HOME);

        return new UrlGenerator($stack, $router);
    }
}
