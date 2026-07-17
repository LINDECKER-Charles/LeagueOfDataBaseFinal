<?php

declare(strict_types=1);

namespace App\Tests\Unit\Service\Client;

use App\Service\Client\ClientManager;
use App\Service\Client\VersionManager;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;

/**
 * Exercises the request-cheap locale resolution that drives the UI locale:
 * signed "remember" cookie -> getSelectedLocale(), without starting a session.
 */
final class ClientManagerSelectedLocaleTest extends TestCase
{
    private const SECRET = 'unit-test-secret';

    public function testReadsLocaleFromSignedRememberCookie(): void
    {
        $manager = $this->manager($this->requestWithCookie($this->signedCookie('es_ES', '15.1.1')));

        self::assertSame('es_ES', $manager->getSelectedLocale());
    }

    public function testIgnoresTamperedCookie(): void
    {
        $raw = $this->signedCookie('es_ES', '15.1.1');
        $tampered = str_replace('|', 'x|', $raw); // break the HMAC

        $manager = $this->manager($this->requestWithCookie($tampered));

        self::assertNull($manager->getSelectedLocale());
    }

    public function testReturnsNullWithoutCookieOrSession(): void
    {
        $manager = $this->manager(new Request());

        self::assertNull($manager->getSelectedLocale());
    }

    private function manager(Request $request): ClientManager
    {
        $stack = new RequestStack();
        $stack->push($request);

        $versionManager = $this->createMock(VersionManager::class);

        return new ClientManager(
            $stack,
            $versionManager,
            self::SECRET,
            'en_US',
        );
    }

    private function requestWithCookie(string $value): Request
    {
        return new Request(cookies: ['lod_prefs' => $value]);
    }

    /** Mirrors ClientManager::makeRememberCookie() payload format. */
    private function signedCookie(string $locale, ?string $version): string
    {
        $json = json_encode(['l' => $locale, 'v' => $version], JSON_UNESCAPED_SLASHES);
        $sig = hash_hmac('sha256', $json, self::SECRET);

        return base64_encode($json) . '|' . $sig;
    }
}
