<?php
declare(strict_types=1);

namespace App\Service\Tools;

use Symfony\Contracts\HttpClient\HttpClientInterface;

/**
 * Client for the Go fetch gateway (go-fetcher).
 *
 * All Riot Data Dragon egress goes through this service. Bodies are base64-encoded
 * over the wire so JSON and binary (images) share a single contract; the gateway
 * fetches batches in parallel.
 */
final class GoFetcherClient
{
    /**
     * Max URLs per POST /fetch. Kept safely below the gateway's MAX_URLS_PER_REQUEST
     * (default 512) and bounds the request body (1 MiB cap) and the base64 response
     * size regardless of how many images a resource has.
     */
    private const MAX_URLS_PER_BATCH = 200;

    public function __construct(private readonly HttpClientInterface $http) {}

    /**
     * Fetch a single DDragon URL and return the raw body bytes.
     *
     * @throws \RuntimeException on transport error, upstream non-2xx or invalid payload.
     */
    public function fetch(string $url): string
    {
        try {
            $data = $this->http->request('POST', '/fetch', ['json' => ['urls' => [$url]]])->toArray();
        } catch (\Throwable $e) {
            throw new \RuntimeException('go-fetcher: request failed: '.$e->getMessage(), 0, $e);
        }

        $item = $data['results'][0] ?? null;
        if (!is_array($item)) {
            throw new \RuntimeException('go-fetcher: empty result for '.$url);
        }

        return $this->decodeItem($item, $url);
    }

    /**
     * Fetch many DDragon URLs in parallel.
     *
     * @param string[] $urls
     * @return array<string,string> url => raw bytes (failed URLs are omitted)
     */
    public function fetchMany(array $urls): array
    {
        $urls = array_values(array_unique($urls));
        if ($urls === []) {
            return [];
        }

        // Chunk so a resource with more images than the gateway allows per request
        // (e.g. items) still resolves in bounded batches instead of failing 413.
        $out = [];
        foreach (array_chunk($urls, self::MAX_URLS_PER_BATCH) as $chunk) {
            $out += $this->fetchBatch($chunk);
        }

        return $out;
    }

    /**
     * @param string[] $urls  already unique, size <= MAX_URLS_PER_BATCH
     * @return array<string,string> url => raw bytes (failed URLs are omitted)
     */
    private function fetchBatch(array $urls): array
    {
        try {
            $data = $this->http->request('POST', '/fetch', ['json' => ['urls' => $urls]])->toArray();
        } catch (\Throwable $e) {
            throw new \RuntimeException('go-fetcher: batch request failed: '.$e->getMessage(), 0, $e);
        }

        $out = [];
        foreach ($data['results'] ?? [] as $item) {
            if (!is_array($item) || isset($item['error'])) {
                continue;
            }
            $status = (int) ($item['status'] ?? 0);
            if ($status < 200 || $status >= 300) {
                continue;
            }
            $bytes = base64_decode((string) ($item['body_base64'] ?? ''), true);
            if ($bytes !== false && isset($item['url'])) {
                $out[(string) $item['url']] = $bytes;
            }
        }

        return $out;
    }

    /**
     * DDragon version list (most recent first), via the gateway passthrough.
     *
     * @return array<int,string>
     */
    public function versions(): array
    {
        return $this->http->request('GET', '/versions')->toArray();
    }

    /**
     * DDragon available data languages, via the gateway passthrough.
     *
     * @return array<int,string>
     */
    public function languages(): array
    {
        return $this->http->request('GET', '/languages')->toArray();
    }

    /**
     * @param array<string,mixed> $item
     */
    private function decodeItem(array $item, string $url): string
    {
        if (isset($item['error'])) {
            throw new \RuntimeException('go-fetcher: '.$item['error']);
        }
        $status = (int) ($item['status'] ?? 0);
        if ($status < 200 || $status >= 300) {
            throw new \RuntimeException(sprintf('go-fetcher: upstream status %d for %s', $status, $url));
        }
        $bytes = base64_decode((string) ($item['body_base64'] ?? ''), true);
        if ($bytes === false) {
            throw new \RuntimeException('go-fetcher: invalid base64 body for '.$url);
        }

        return $bytes;
    }
}
