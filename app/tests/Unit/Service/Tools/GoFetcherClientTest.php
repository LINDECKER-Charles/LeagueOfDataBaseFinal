<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Tools;

use App\Service\Tools\GoFetcherClient;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpClient\MockHttpClient;
use Symfony\Component\HttpClient\Response\MockResponse;

final class GoFetcherClientTest extends TestCase
{
    private function json(array $payload): MockResponse
    {
        return new MockResponse(json_encode($payload), ['response_headers' => ['content-type' => 'application/json']]);
    }

    public function testFetchDecodesBase64Body(): void
    {
        $client = new MockHttpClient($this->json(['results' => [[
            'url' => 'https://ddragon.leagueoflegends.com/x.json',
            'status' => 200,
            'content_type' => 'application/json',
            'body_base64' => base64_encode('{"ok":true}'),
        ]]]));

        $go = new GoFetcherClient($client);

        self::assertSame('{"ok":true}', $go->fetch('https://ddragon.leagueoflegends.com/x.json'));
    }

    public function testFetchThrowsOnUpstreamError(): void
    {
        $client = new MockHttpClient($this->json(['results' => [[
            'url' => 'https://ddragon.leagueoflegends.com/x.json',
            'error' => 'host not allowed',
        ]]]));

        $this->expectException(\RuntimeException::class);
        (new GoFetcherClient($client))->fetch('https://ddragon.leagueoflegends.com/x.json');
    }

    public function testFetchManyReturnsOnlySuccessfulEntries(): void
    {
        $client = new MockHttpClient($this->json(['results' => [
            ['url' => 'https://ddragon.leagueoflegends.com/a.png', 'status' => 200, 'body_base64' => base64_encode('A')],
            ['url' => 'https://ddragon.leagueoflegends.com/b.png', 'error' => 'boom'],
            ['url' => 'https://ddragon.leagueoflegends.com/c.png', 'status' => 404, 'body_base64' => base64_encode('missing')],
        ]]));

        $out = (new GoFetcherClient($client))->fetchMany([
            'https://ddragon.leagueoflegends.com/a.png',
            'https://ddragon.leagueoflegends.com/b.png',
            'https://ddragon.leagueoflegends.com/c.png',
        ]);

        self::assertSame(['https://ddragon.leagueoflegends.com/a.png' => 'A'], $out);
    }

    public function testFetchManySplitsLargeBatchesUnderTheGatewayLimit(): void
    {
        $urls = array_map(
            static fn (int $i): string => "https://ddragon.leagueoflegends.com/img/item/$i.png",
            range(1, 250),
        );

        $batchSizes = [];
        $client = new MockHttpClient(function (string $method, string $url, array $options) use (&$batchSizes): MockResponse {
            $chunk = json_decode((string) $options['body'], true)['urls'];
            $batchSizes[] = count($chunk);

            return $this->json(['results' => array_map(
                static fn (string $u): array => ['url' => $u, 'status' => 200, 'body_base64' => base64_encode('x')],
                $chunk,
            )]);
        });

        $out = (new GoFetcherClient($client))->fetchMany($urls);

        self::assertCount(250, $out, 'every URL across all chunks is resolved');
        self::assertSame([200, 50], $batchSizes, 'a >200 batch is split into <=200-URL requests');
    }

    public function testVersionsPassthrough(): void
    {
        $client = new MockHttpClient($this->json(['15.1.1', '15.0.1']));

        self::assertSame(['15.1.1', '15.0.1'], (new GoFetcherClient($client))->versions());
    }
}
