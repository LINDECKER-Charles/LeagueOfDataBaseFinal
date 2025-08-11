<?php

declare(strict_types=1);

namespace App\Tests\Unit\Service;

use App\Service\APICaller;
use App\Service\VersionManager;
use PHPUnit\Framework\TestCase;
use Psr\Log\LoggerInterface;
use Symfony\Component\HttpClient\MockHttpClient;
use Symfony\Component\HttpClient\Response\MockResponse;
use Symfony\Contracts\Cache\CacheInterface;
use Symfony\Contracts\Cache\ItemInterface;

/**
 * Petit cache en mémoire pour les tests qui implémente CacheInterface
 * et capture la valeur passée à expiresAfter().
 */
final class InMemoryTestCache implements CacheInterface
{
    /** @var array<string,mixed> */
    public array $store = [];

    /** @var array<string,int|null> TTL capturé pour chaque clé (en secondes) */
    public array $ttl = [];

    public function get(string $key, callable $callback, float $beta = null, array &$metadata = null): mixed
    {
        if (!array_key_exists($key, $this->store)) {
            $item = new class implements ItemInterface {
                public ?int $expiresAfter = null;

                public function getKey(): string { return ''; }
                public function get(): mixed { return null; }
                public function isHit(): bool { return false; }
                public function set(mixed $value): static { return $this; }
                public function expiresAt(?\DateTimeInterface $expiration): static { return $this; }
                public function expiresAfter(int|\DateInterval|null $time): static {
                    // Normalise en secondes si DateInterval, sinon int|null
                    if ($time instanceof \DateInterval) {
                        $ref = new \DateTimeImmutable('@0');
                        $end = $ref->add($time);
                        $this->expiresAfter = $end->getTimestamp();
                    } else {
                        $this->expiresAfter = $time;
                    }
                    return $this;
                }
            };

            $value = $callback($item);
            $this->store[$key] = $value;
            $this->ttl[$key] = $item->expiresAfter;
        }

        return $this->store[$key];
    }

    public function delete(string $key): bool
    {
        $existed = array_key_exists($key, $this->store);
        unset($this->store[$key], $this->ttl[$key]);
        return $existed;
    }
}

final class VersionManagerTest extends TestCase
{
    private function makeService(MockHttpClient $http, InMemoryTestCache $cache, LoggerInterface $logger): VersionManager
    {
        // APICaller est injecté mais non utilisé par VersionManager -> on donne un vrai APICaller isolé
        $dummyApiCaller = new APICaller(new MockHttpClient());
        return new VersionManager($http, $cache, $logger, $dummyApiCaller);
    }

    public function test_getVersions_uses_http_once_and_caches_with_10min_ttl(): void
    {
        $requestCount = 0;
        $http = new MockHttpClient(function (string $method, string $url) use (&$requestCount) {
            $requestCount++;
            $this->assertSame('GET', $method);
            $this->assertStringContainsString('versions.json', $url);
            return new MockResponse(json_encode(['15.1.1', '15.1.0'], JSON_THROW_ON_ERROR));
        });

        $cache = new InMemoryTestCache();
        $logger = $this->createMock(LoggerInterface::class);
        $svc = $this->makeService($http, $cache, $logger);

        $a1 = $svc->getVersions();
        $a2 = $svc->getVersions(); // doit venir du cache

        $this->assertSame(['15.1.1', '15.1.0'], $a1);
        $this->assertSame($a1, $a2, 'La deuxième lecture doit venir du cache');
        $this->assertSame(1, $requestCount, 'Un seul appel HTTP attendu grâce au cache');

        $this->assertArrayHasKey('riot_versions', $cache->ttl);
        $this->assertSame(600, $cache->ttl['riot_versions'], 'TTL attendu: 600s (10 minutes)');
    }

    public function test_getVersions_on_exception_logs_and_returns_empty(): void
    {
        $http = new MockHttpClient(function () {
            throw new \RuntimeException('boom');
        });

        $cache = new InMemoryTestCache();

        $logger = $this->createMock(LoggerInterface::class);
        $logger->expects($this->once())
            ->method('error')
            ->with($this->stringContains('Erreur lors de la récupération des versions Riot'), $this->arrayHasKey('message'));

        $svc = $this->makeService($http, $cache, $logger);

        $out = $svc->getVersions();
        $this->assertSame([], $out, 'En cas d’exception, on doit retourner []');
    }

    public function test_getLanguages_uses_http_once_and_caches_with_1month_ttl(): void
    {
        $requestCount = 0;
        $http = new MockHttpClient(function (string $method, string $url) use (&$requestCount) {
            $requestCount++;
            $this->assertSame('GET', $method);
            $this->assertStringContainsString('languages.json', $url);
            return new MockResponse(json_encode(['fr_FR', 'en_US'], JSON_THROW_ON_ERROR));
        });

        $cache = new InMemoryTestCache();
        $logger = $this->createMock(LoggerInterface::class);
        $svc = $this->makeService($http, $cache, $logger);

        $a1 = $svc->getLanguages();
        $a2 = $svc->getLanguages(); // cache

        $this->assertSame(['fr_FR', 'en_US'], $a1);
        $this->assertSame($a1, $a2);
        $this->assertSame(1, $requestCount, 'Un seul appel HTTP attendu');

        $this->assertArrayHasKey('riot_languages', $cache->ttl);
        $this->assertSame(2592000, $cache->ttl['riot_languages'], 'TTL attendu: 2 592 000s (1 mois)');
    }

    public function test_versionExists_and_languageExists(): void
    {
        // Réponses séquentielles: 1) versions.json 2) languages.json
        $http = new MockHttpClient([
            new MockResponse(json_encode(['15.1.1', '15.1.0'], JSON_THROW_ON_ERROR)),
            new MockResponse(json_encode(['fr_FR', 'en_US'], JSON_THROW_ON_ERROR)),
        ]);

        $cache = new InMemoryTestCache();
        $logger = $this->createMock(LoggerInterface::class);
        $svc = $this->makeService($http, $cache, $logger);

        $this->assertTrue($svc->versionExists('15.1.1'));
        $this->assertFalse($svc->versionExists('0.0.0'));
        $this->assertFalse($svc->versionExists(null));
        $this->assertFalse($svc->versionExists(''));

        $this->assertTrue($svc->languageExists('fr_FR'));
        $this->assertFalse($svc->languageExists('xx_YY'));
        $this->assertFalse($svc->languageExists(null));
        $this->assertFalse($svc->languageExists(''));
    }

    public function test_languageExists_fallbacks_to_labels_when_api_empty(): void
    {
        // Simule API KO pour languages => VersionManager doit fallback sur getLanguageLabels()
        $http = new MockHttpClient(function (string $method, string $url) {
            if (str_contains($url, 'languages.json')) {
                throw new \RuntimeException('unreachable');
            }
            // Pour éviter des surprises, renvoyer aussi quelque chose si getVersions() est appelé
            return new MockResponse(json_encode(['15.1.1'], JSON_THROW_ON_ERROR));
        });

        $cache = new InMemoryTestCache();
        $logger = $this->createMock(LoggerInterface::class);
        $svc = $this->makeService($http, $cache, $logger);

        $this->assertTrue($svc->languageExists('fr_FR'), 'Doit être trouvé via les labels internes');
        $this->assertFalse($svc->languageExists('xx_YY'));
    }

    public function test_validateSelection_reports_errors(): void
    {
        // 1) versions.json -> ["15.1.1"]
        // 2) languages.json -> ["fr_FR"]
        $http = new MockHttpClient([
            new MockResponse(json_encode(['15.1.1'], JSON_THROW_ON_ERROR)),
            new MockResponse(json_encode(['fr_FR'], JSON_THROW_ON_ERROR)),
        ]);

        $cache = new InMemoryTestCache();
        $logger = $this->createMock(LoggerInterface::class);
        $svc = $this->makeService($http, $cache, $logger);

        $ok = $svc->validateSelection('15.1.1', 'fr_FR');
        $this->assertTrue($ok['ok']);
        $this->assertSame([], $ok['errors']);

        $bad = $svc->validateSelection('0.0.0', 'xx_YY');
        $this->assertFalse($bad['ok']);
        $this->assertArrayHasKey('version', $bad['errors']);
        $this->assertArrayHasKey('language', $bad['errors']);
    }
}
