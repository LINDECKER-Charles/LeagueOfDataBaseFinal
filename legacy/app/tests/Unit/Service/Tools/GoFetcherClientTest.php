<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Tools;

use App\Service\Tools\GoFetcherClient;
use App\Service\Tools\UpstreamNotFoundException;
use App\Tests\Unit\Support\RecordingLogger;
use PHPUnit\Framework\TestCase;
use Psr\Log\LogLevel;
use Psr\Log\NullLogger;
use Symfony\Component\HttpClient\MockHttpClient;
use Symfony\Component\HttpClient\Response\MockResponse;

final class GoFetcherClientTest extends TestCase
{
    private function json(array $payload): MockResponse
    {
        return new MockResponse(
            json_encode($payload),
            ['response_headers' => ['content-type' => 'application/json']],
        );
    }

    public function testFetchDecodesBase64Body(): void
    {
        $client = new MockHttpClient($this->json(['results' => [[
            'url' => 'https://ddragon.leagueoflegends.com/x.json',
            'status' => 200,
            'content_type' => 'application/json',
            'body_base64' => base64_encode('{"ok":true}'),
        ]]]));

        $go = new GoFetcherClient($client, new NullLogger());

        self::assertSame('{"ok":true}', $go->fetch('https://ddragon.leagueoflegends.com/x.json'));
    }

    public function testFetchThrowsOnUpstreamError(): void
    {
        $client = new MockHttpClient($this->json(['results' => [[
            'url' => 'https://ddragon.leagueoflegends.com/x.json',
            'error' => 'host not allowed',
        ]]]));

        $this->expectException(\RuntimeException::class);
        (new GoFetcherClient($client, new NullLogger()))->fetch('https://ddragon.leagueoflegends.com/x.json');
    }

    /**
     * A gateway that answers 200 for every URL of the batch it receives, and
     * announces each batch size so chunking can be asserted.
     *
     * @param callable(int):void $onBatch
     */
    private function echoingClient(callable $onBatch): MockHttpClient
    {
        return new MockHttpClient(
            function (string $method, string $url, array $options) use ($onBatch): MockResponse {
                $chunk = json_decode((string) $options['body'], true)['urls'];
                $onBatch(count($chunk));

                return $this->json(['results' => array_map(
                    static fn (string $u): array => [
                        'url' => $u,
                        'status' => 200,
                        'body_base64' => base64_encode('x'),
                    ],
                    $chunk,
                )]);
            },
        );
    }

    private function fetchWithStatus(int $status): void
    {
        $client = new MockHttpClient($this->json(['results' => [[
            'url' => 'https://ddragon.leagueoflegends.com/x.json',
            'status' => $status,
        ]]]));
        (new GoFetcherClient($client, new NullLogger()))->fetch('https://ddragon.leagueoflegends.com/x.json');
    }

    /** 403 = the resource is permanently gone upstream → dedicated typed exception. */
    public function testFetchThrowsNotFoundOn403(): void
    {
        $this->expectException(UpstreamNotFoundException::class);
        $this->fetchWithStatus(403);
    }

    /** 404 = the resource is permanently gone upstream → dedicated typed exception. */
    public function testFetchThrowsNotFoundOn404(): void
    {
        $this->expectException(UpstreamNotFoundException::class);
        $this->fetchWithStatus(404);
    }

    /** A transient failure (5xx) stays a generic RuntimeException, not an absence. */
    public function testFetchThrowsGenericRuntimeOnTransientUpstream(): void
    {
        $client = new MockHttpClient($this->json(['results' => [[
            'url' => 'https://ddragon.leagueoflegends.com/x.json',
            'status' => 503,
        ]]]));

        $go = new GoFetcherClient($client, new NullLogger());
        try {
            $go->fetch('https://ddragon.leagueoflegends.com/x.json');
            self::fail('expected an exception');
        } catch (\RuntimeException $e) {
            self::assertNotInstanceOf(UpstreamNotFoundException::class, $e);
        }
    }

    public function testFetchManyReturnsOnlySuccessfulEntries(): void
    {
        $client = new MockHttpClient($this->json(['results' => [
            [
                'url' => 'https://ddragon.leagueoflegends.com/a.png',
                'status' => 200,
                'body_base64' => base64_encode('A'),
            ],
            ['url' => 'https://ddragon.leagueoflegends.com/b.png', 'error' => 'boom'],
            [
                'url' => 'https://ddragon.leagueoflegends.com/c.png',
                'status' => 404,
                'body_base64' => base64_encode('missing'),
            ],
        ]]));

        $out = (new GoFetcherClient($client, new NullLogger()))->fetchMany([
            'https://ddragon.leagueoflegends.com/a.png',
            'https://ddragon.leagueoflegends.com/b.png',
            'https://ddragon.leagueoflegends.com/c.png',
        ]);

        self::assertSame(['https://ddragon.leagueoflegends.com/a.png' => 'A'], $out['bytes']);
        // c: clean upstream 404 → definitive absence. b: gateway error → transient,
        // reported in neither channel so a later call retries it.
        self::assertSame(['https://ddragon.leagueoflegends.com/c.png'], $out['absent']);
    }

    public function testFetchManySplitsLargeBatchesUnderTheGatewayLimit(): void
    {
        $urls = array_map(
            static fn (int $i): string => "https://ddragon.leagueoflegends.com/img/item/$i.png",
            range(1, 250),
        );

        $batchSizes = [];
        $client = $this->echoingClient(static function (int $size) use (&$batchSizes): void {
            $batchSizes[] = $size;
        });

        $out = (new GoFetcherClient($client, new NullLogger()))->fetchMany($urls);

        self::assertCount(250, $out['bytes'], 'every URL across all chunks is resolved');
        self::assertSame([200, 50], $batchSizes, 'a >200 batch is split into <=200-URL requests');
    }

    public function testVersionsPassthrough(): void
    {
        $client = new MockHttpClient($this->json(['15.1.1', '15.0.1']));

        self::assertSame(['15.1.1', '15.0.1'], (new GoFetcherClient($client, new NullLogger()))->versions());
    }

    /**
     * `fetchBatch()` drops an unusable result on the floor, so a batch where
     * NOTHING resolved is byte-for-byte identical to a batch that had nothing to
     * do. `error`, per the level table: no image at all came back.
     */
    public function testABatchWhereNothingResolvedIsReportedAsAnError(): void
    {
        $logger = new RecordingLogger();
        $client = new MockHttpClient($this->json(['results' => [
            ['url' => 'https://ddragon.leagueoflegends.com/a.png', 'error' => 'upstream 502'],
            ['url' => 'https://ddragon.leagueoflegends.com/b.png', 'status' => 503],
        ]]));

        (new GoFetcherClient($client, $logger))->fetchMany([
            'https://ddragon.leagueoflegends.com/a.png',
            'https://ddragon.leagueoflegends.com/b.png',
        ]);

        $record = $logger->only('catalog.fetch_batch.unresolved');

        self::assertSame(LogLevel::ERROR, $record['level']);
        self::assertSame(2, $record['context']['requested']);
        self::assertSame(0, $record['context']['resolved']);
        self::assertSame(2, $record['context']['unresolved']);
    }

    /** Partial: the visitor still gets images, the next visit retries the rest. */
    public function testAPartiallyResolvedBatchIsOnlyAWarning(): void
    {
        $logger = new RecordingLogger();
        $client = new MockHttpClient($this->json(['results' => [
            [
                'url' => 'https://ddragon.leagueoflegends.com/a.png',
                'status' => 200,
                'body_base64' => base64_encode('png'),
            ],
            ['url' => 'https://ddragon.leagueoflegends.com/b.png', 'status' => 503],
        ]]));

        (new GoFetcherClient($client, $logger))->fetchMany([
            'https://ddragon.leagueoflegends.com/a.png',
            'https://ddragon.leagueoflegends.com/b.png',
        ]);

        self::assertSame(LogLevel::WARNING, $logger->only('catalog.fetch_batch.unresolved')['level']);
    }

    /**
     * A 403/404 is a DEFINITIVE absence the caller persists, not a failure: the
     * runes of patches 7.22-8.7 would otherwise log on every single warm.
     */
    public function testADefinitiveAbsenceIsNotReportedAsUnresolved(): void
    {
        $logger = new RecordingLogger();
        $client = new MockHttpClient($this->json(['results' => [
            ['url' => 'https://ddragon.leagueoflegends.com/gone.dds', 'status' => 404],
        ]]));

        (new GoFetcherClient($client, $logger))->fetchMany([
            'https://ddragon.leagueoflegends.com/gone.dds',
        ]);

        self::assertSame([], $logger->keys());
    }

    /** A batch that fully resolved must stay silent. */
    public function testAFullyResolvedBatchLogsNothing(): void
    {
        $logger = new RecordingLogger();
        $client = new MockHttpClient($this->json(['results' => [[
            'url' => 'https://ddragon.leagueoflegends.com/a.png',
            'status' => 200,
            'body_base64' => base64_encode('png'),
        ]]]));

        (new GoFetcherClient($client, $logger))->fetchMany([
            'https://ddragon.leagueoflegends.com/a.png',
        ]);

        self::assertSame([], $logger->keys());
    }
}
