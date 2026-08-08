<?php

declare(strict_types=1);

namespace App\Tests\Unit\Service\Client;

use App\Service\Client\ClientManager;
use App\Service\Client\VersionCatalogInterface;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Session\Session;
use Symfony\Component\HttpFoundation\Session\Storage\MockArraySessionStorage;

/**
 * getSession() turns what the visitor's session remembers into the selection a
 * page renders with. Patch and language are two independent axes: either one can
 * disappear from the catalog between two visits (a patch is delisted, a locale is
 * dropped) without the other being any less valid.
 */
final class ClientManagerSelectionTest extends TestCase
{
    private const KNOWN_VERSION = '15.1.1';
    private const LATEST_VERSION = '15.2.1';
    private const DEFAULT_LOCALE = 'en_US';

    public function testAnUnknownLanguageDoesNotAlsoResetAValidVersion(): void
    {
        $manager = $this->manager(self::KNOWN_VERSION, 'xx_XX');

        self::assertSame(
            ['version' => self::KNOWN_VERSION, 'lang' => self::DEFAULT_LOCALE],
            $manager->getSession(),
        );
    }

    public function testAnUnknownVersionDoesNotAlsoResetAValidLanguage(): void
    {
        $manager = $this->manager('9.9.9', 'fr_FR');

        self::assertSame(
            ['version' => self::LATEST_VERSION, 'lang' => 'fr_FR'],
            $manager->getSession(),
        );
    }

    public function testAKnownSelectionIsReturnedUntouched(): void
    {
        $manager = $this->manager(self::KNOWN_VERSION, 'fr_FR');

        self::assertSame(
            ['version' => self::KNOWN_VERSION, 'lang' => 'fr_FR'],
            $manager->getSession(),
        );
    }

    public function testAnUnavailableCatalogYieldsAnEmptyVersionNotNull(): void
    {
        // Upstream outage: the catalog knows no patch at all. The contract still
        // promises two strings — a null here used to reach every template.
        $manager = $this->manager('9.9.9', 'fr_FR', latest: '');

        self::assertSame(['version' => '', 'lang' => 'fr_FR'], $manager->getSession());
    }

    private function manager(
        string $sessionVersion,
        string $sessionLocale,
        string $latest = self::LATEST_VERSION,
    ): ClientManager {
        $session = new Session(new MockArraySessionStorage());
        $session->set('_locale', $sessionLocale);
        $session->set('dd_version', $sessionVersion);

        $request = new Request();
        $request->setSession($session);

        $stack = new RequestStack();
        $stack->push($request);

        return new ClientManager($stack, $this->catalog($latest), 'secret', self::DEFAULT_LOCALE);
    }

    private function catalog(string $latest): VersionCatalogInterface
    {
        $catalog = $this->createStub(VersionCatalogInterface::class);
        $catalog->method('versionExists')
            ->willReturnCallback(static fn (?string $v): bool => $v === self::KNOWN_VERSION);
        $catalog->method('languageExists')
            ->willReturnCallback(
                static fn (?string $l): bool => in_array($l, ['en_US', 'fr_FR'], true),
            );
        $catalog->method('latestVersion')->willReturn($latest);

        return $catalog;
    }
}
