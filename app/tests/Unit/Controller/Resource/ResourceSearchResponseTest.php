<?php
declare(strict_types=1);

namespace App\Tests\Unit\Controller\Resource;

use App\Controller\Resource\AbstractResourceController;
use App\Service\API\CategoriesInterface;
use App\Service\API\DatasetRef;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Tools\GoFetcherClient;
use App\Tests\Unit\Support\RecordingLogger;
use PHPUnit\Framework\TestCase;
use Psr\Log\LogLevel;
use Psr\Log\NullLogger;
use Symfony\Component\Cache\Adapter\ArrayAdapter;
use Symfony\Component\DependencyInjection\Container;
use Symfony\Component\HttpClient\MockHttpClient;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;

/**
 * Contract of the name-search endpoints shared by the champion, item and
 * summoner controllers: a JSON list of {id, name, image} on success, and a
 * real failure STATUS when the data layer is down — it used to answer 200 with a
 * bare french sentence carrying the raw exception message, which no client could
 * tell apart from a successful (differently shaped) payload.
 */
final class ResourceSearchResponseTest extends TestCase
{
    private const VERSION = '15.1.1';
    private const LANG = 'en_US';

    public function testADataLayerFailureIsAnErrorStatusAndLeaksNothing(): void
    {
        $manager = $this->createStub(CategoriesInterface::class);
        $manager->method('searchByName')
            ->willThrowException(new \RuntimeException('minio: connection refused at 10.0.0.4'));

        $response = $this->search($manager);
        $payload = $this->decode($response);

        self::assertSame(Response::HTTP_SERVICE_UNAVAILABLE, $response->getStatusCode());
        self::assertArrayHasKey('error', $payload);
        self::assertStringNotContainsString('minio', $payload['error']);
        self::assertStringNotContainsString('10.0.0.4', $payload['error']);
    }

    /**
     * The 503 body is deliberately neutral, so without a log line the outage is
     * invisible server-side. `error`, not `warning`: this endpoint is only ever
     * called by our own front-end, with a selection it already validated.
     */
    public function testADataLayerFailureIsReportedOnTheCatalogChannel(): void
    {
        $logger = new RecordingLogger();
        $manager = $this->createStub(CategoriesInterface::class);
        $manager->method('searchByName')
            ->willThrowException($boom = new \RuntimeException('minio down'));

        $this->search($manager, logger: $logger);

        $record = $logger->only('catalog.search.unavailable');

        self::assertSame(LogLevel::ERROR, $record['level']);
        self::assertSame(self::VERSION, $record['context']['version']);
        self::assertSame($boom, $record['context']['exception']);
    }

    /** A search that works must stay silent: volume is what makes a log a signal. */
    public function testASuccessfulSearchLogsNothing(): void
    {
        $logger = new RecordingLogger();
        $manager = $this->createStub(CategoriesInterface::class);
        $manager->method('searchByName')->willReturn([]);

        $this->search($manager, logger: $logger);

        self::assertSame([], $logger->keys());
    }

    public function testNoMatchIsAnEmptyListNotAnError(): void
    {
        $manager = $this->createStub(CategoriesInterface::class);
        $manager->method('searchByName')->willReturn([]);

        $response = $this->search($manager);

        self::assertSame(Response::HTTP_OK, $response->getStatusCode());
        self::assertSame([], $this->decode($response));
    }

    public function testMatchesAreProjectedWithTheImageTheCallerResolved(): void
    {
        $manager = $this->createStub(CategoriesInterface::class);
        $manager->method('searchByName')->willReturn([
            ['id' => 'Ahri', 'name' => 'Ahri'],
            ['id' => 'Akali', 'name' => 'Akali'],
        ]);
        $manager->method('getImages')->willReturn(['Ahri' => '/img/ahri.png']);

        $response = $this->search(
            $manager,
            // Keyed by id, like the item/summoner managers — an unresolved row
            // keeps a null image.
            static fn (array $rows, array $images): array => array_map(
                static fn (array $row): ?string => $images[$row['id']] ?? null,
                $rows,
            ),
        );

        self::assertSame([
            ['id' => 'Ahri', 'name' => 'Ahri', 'image' => '/img/ahri.png'],
            ['id' => 'Akali', 'name' => 'Akali', 'image' => null],
        ], $this->decode($response));
    }

    /**
     * A hit that knows its edition (item/summoner managers) keeps it in the
     * payload — "Flash" twice is ambiguous otherwise — while anything else the
     * manager attached to the row stays private.
     */
    public function testAnEditionCarriedByTheHitIsExposedAndNothingElseIs(): void
    {
        $manager = $this->createStub(CategoriesInterface::class);
        $manager->method('searchByName')->willReturn([
            ['id' => 'SummonerFlash', 'name' => 'Flash', 'edition' => 'modern', 'modes' => []],
            ['id' => 'SummonerFlash_Jade', 'name' => 'Flash', 'edition' => 'classic', 'modes' => []],
        ]);
        $manager->method('getImages')->willReturn([]);

        $response = $this->search($manager, static fn (array $rows): array => [null, null]);

        self::assertSame([
            ['id' => 'SummonerFlash', 'name' => 'Flash', 'image' => null, 'edition' => 'modern'],
            [
                'id' => 'SummonerFlash_Jade', 'name' => 'Flash', 'image' => null,
                'edition' => 'classic',
            ],
        ], $this->decode($response));
    }

    public function testTheResultCountIsCappedForEveryResource(): void
    {
        $seen = null;
        $manager = $this->createStub(CategoriesInterface::class);
        $manager->method('searchByName')->willReturnCallback(
            static function (string $n, DatasetRef $d, int $max) use (&$seen): array {
                $seen = $max;

                return [];
            },
        );

        $this->search($manager);

        self::assertSame(20, $seen, 'the endpoints must not stream the whole dataset');
    }

    private function search(
        CategoriesInterface $manager,
        ?callable $imagesFor = null,
        ?RecordingLogger $logger = null,
    ): JsonResponse {
        return $this->controller($logger)->search(
            $manager,
            'ah',
            $imagesFor ?? static fn (array $rows, array $images): array => array_values($images),
        );
    }

    /** @return array<mixed> */
    private function decode(JsonResponse $response): array
    {
        return json_decode((string) $response->getContent(), true, flags: JSON_THROW_ON_ERROR);
    }

    private function controller(?RecordingLogger $logger = null): object
    {
        $stack = new RequestStack();
        $stack->push(Request::create(
            '/api/champions/search/ah?version=' . self::VERSION . '&lang=' . self::LANG,
        ));

        $versions = new VersionManager($this->noEgress(), $this->catalogCache(), new NullLogger());
        $clients = new ClientManager($stack, $versions, 'secret', self::LANG);

        $controller = new class (
            $versions,
            $clients,
            new PageContextResolver($stack, $clients, $versions),
            $stack,
            $logger ?? new NullLogger(),
        ) extends AbstractResourceController {
            public function search(
                CategoriesInterface $manager,
                string $name,
                callable $imagesFor,
            ): JsonResponse {
                return $this->searchResponse($manager, $name, $imagesFor);
            }
        };
        $controller->setContainer(new Container());

        return $controller;
    }

    /** Catalog pre-seeded in the cache: the gateway must never be reached here. */
    private function catalogCache(): ArrayAdapter
    {
        $cache = new ArrayAdapter();
        $catalog = ['riot_versions' => [self::VERSION], 'riot_languages' => [self::LANG]];
        foreach ($catalog as $key => $value) {
            $item = $cache->getItem($key);
            $item->set($value);
            $cache->save($item);
        }

        return $cache;
    }

    private function noEgress(): GoFetcherClient
    {
        return new GoFetcherClient(new MockHttpClient(static function (): void {
            throw new \RuntimeException('the search endpoint must not touch the gateway');
        }), new NullLogger());
    }
}
