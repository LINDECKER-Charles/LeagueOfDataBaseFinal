<?php

declare(strict_types=1);

namespace App\Tests\Unit\Service\Client;

use App\Service\Client\Theme;
use App\Service\Client\ThemeResolver;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;

/**
 * The theme cookie is written by JavaScript and therefore never trusted: these
 * tests pin the two behaviours that keep an unsigned, visitor-writable value
 * safe — anything unrecognised degrades to the default identity, and no branch
 * can throw. They also pin that the two identities paint DIFFERENT browser
 * chrome, since a copy-paste there would silently frame one theme in the
 * other's black.
 */
final class ThemeTest extends TestCase
{
    /** @return iterable<string, array{?string, Theme}> */
    public static function cookieValues(): iterable
    {
        yield 'known default' => ['hextech', Theme::Hextech];
        yield 'zaun' => ['zaun', Theme::Zaun];
        yield 'noxus' => ['noxus', Theme::Noxus];
        yield 'hyphenated slug' => ['spirit-blossom', Theme::SpiritBlossom];
        yield 'absent' => [null, Theme::Hextech];
        yield 'empty' => ['', Theme::Hextech];
        yield 'unknown identity' => ['demacia', Theme::Hextech];
        yield 'wrong case' => ['Zaun', Theme::Hextech];
        yield 'underscored slug' => ['spirit_blossom', Theme::Hextech];
        yield 'injection attempt' => ['zaun"] body {display:none}', Theme::Hextech];
    }

    #[DataProvider('cookieValues')]
    public function testAnyUnrecognisedCookieFallsBackToTheDefault(
        ?string $cookie,
        Theme $expected,
    ): void {
        self::assertSame($expected, Theme::fromCookie($cookie));
    }

    public function testEachIdentityPaintsItsOwnBrowserChrome(): void
    {
        $colors = array_map(
            static fn (Theme $theme): string => $theme->browserColor(),
            Theme::cases(),
        );

        self::assertSame($colors, array_unique($colors));
        foreach ($colors as $color) {
            self::assertMatchesRegularExpression('/^#[0-9a-f]{6}$/', $color);
        }
    }

    public function testEachIdentityHasItsOwnDisplayName(): void
    {
        $labels = array_map(
            static fn (Theme $theme): string => $theme->label(),
            Theme::cases(),
        );

        self::assertSame($labels, array_unique($labels));
        self::assertNotContains('', $labels);
    }

    public function testEveryIdentityNamesWhereItComesFrom(): void
    {
        foreach (Theme::cases() as $theme) {
            self::assertNotSame('', $theme->origin(), $theme->value);
        }
    }

    /**
     * The preload in base.html.twig is driven by this, so a slug that does not
     * resolve to a real file would emit a 404 preload on every page of a theme.
     */
    public function testAlternateIdentitiesPreloadAnExistingDisplayFace(): void
    {
        self::assertNull(Theme::Hextech->displayFontUrl());

        foreach (Theme::cases() as $theme) {
            if ($theme === Theme::Hextech) {
                continue;
            }
            $url = $theme->displayFontUrl();
            self::assertNotNull($url, $theme->value);
            self::assertFileExists(\dirname(__DIR__, 4) . '/public' . $url);
        }
    }

    public function testResolverReadsTheCookieOfTheCurrentRequest(): void
    {
        self::assertSame(Theme::Zaun, $this->resolverFor(['lod_theme' => 'zaun'])->current());
        self::assertSame(Theme::Hextech, $this->resolverFor([])->current());
    }

    /** A request-less context (CLI warm-up, error rendering) must still resolve. */
    public function testResolverFallsBackToTheDefaultWithoutARequest(): void
    {
        self::assertSame(Theme::Hextech, (new ThemeResolver(new RequestStack()))->current());
    }

    public function testResolverOffersEveryIdentityToTheToggle(): void
    {
        self::assertSame(Theme::cases(), $this->resolverFor([])->options());
    }

    /** @param array<string, string> $cookies */
    private function resolverFor(array $cookies): ThemeResolver
    {
        $stack = new RequestStack();
        $stack->push(new Request(cookies: $cookies));

        return new ThemeResolver($stack);
    }
}
