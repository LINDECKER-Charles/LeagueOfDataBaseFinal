<?php

declare(strict_types=1);

namespace App\Tests\Unit\Service\I18n;

use App\Service\I18n\UiLocaleResolver;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;

final class UiLocaleResolverTest extends TestCase
{
    private const ENABLED = [
        'ar', 'cs', 'de', 'el', 'en', 'es', 'fr', 'hu', 'id', 'it', 'ja',
        'ko', 'pl', 'pt', 'ro', 'ru', 'th', 'tr', 'vi', 'zh_Hans', 'zh_Hant',
    ];

    #[DataProvider('mappingProvider')]
    public function testToUiLocale(string $ddragon, string $expected): void
    {
        self::assertSame($expected, $this->resolver()->toUiLocale($ddragon));
    }

    public static function mappingProvider(): iterable
    {
        yield 'french' => ['fr_FR', 'fr'];
        yield 'us english' => ['en_US', 'en'];
        yield 'au english collapses to base' => ['en_AU', 'en'];
        yield 'mexican spanish collapses to base' => ['es_MX', 'es'];
        yield 'brazilian portuguese' => ['pt_BR', 'pt'];
        yield 'chinese simplified (CN)' => ['zh_CN', 'zh_Hans'];
        yield 'chinese simplified (MY)' => ['zh_MY', 'zh_Hans'];
        yield 'chinese traditional (TW)' => ['zh_TW', 'zh_Hant'];
    }

    public function testResolvePrefersMappedLocaleWhenEnabled(): void
    {
        self::assertSame('de', $this->resolver()->resolve('de_DE', 'en'));
        self::assertSame('zh_Hant', $this->resolver()->resolve('zh_TW', 'en'));
    }

    public function testResolveFallsBackWhenLocaleHasNoCatalog(): void
    {
        $resolver = new UiLocaleResolver(['en', 'fr']); // de not shipped
        self::assertSame('fr', $resolver->resolve('de_DE', 'fr'));
    }

    public function testResolveFallsBackOnNullOrEmpty(): void
    {
        self::assertSame('en', $this->resolver()->resolve(null, 'en'));
        self::assertSame('fr', $this->resolver()->resolve('', 'fr'));
    }

    private function resolver(): UiLocaleResolver
    {
        return new UiLocaleResolver(self::ENABLED);
    }
}
