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
     * @throws UpstreamNotFoundException when the resource is definitively absent (403/404).
     * @throws \RuntimeException on transport error, other upstream non-2xx or invalid payload.
     */
    public function fetch(string $url): string
    {
        try {
            $data = $this->http->request('POST', '/fetch', ['json' => ['urls' => [$url]]])
                ->toArray();
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
     * `bytes` carries the successful bodies; `absent` the URLs the CDN answered
     * 403/404 for — immutable absences the caller may persist so they are never
     * asked for again. Transient failures (5xx, gateway error, undecodable
     * body) appear in NEITHER: they stay unresolved and are naturally retried.
     *
     * @param string[] $urls
     * @return array{bytes: array<string,string>, absent: list<string>}
     */
    public function fetchMany(array $urls): array
    {
        $urls = array_values(array_unique($urls));
        if ($urls === []) {
            return ['bytes' => [], 'absent' => []];
        }

        // Chunk so a resource with more images than the gateway allows per request
        // (e.g. items) still resolves in bounded batches instead of failing 413.
        $bytes  = [];
        $absent = [];
        foreach (array_chunk($urls, self::MAX_URLS_PER_BATCH) as $chunk) {
            $batch   = $this->fetchBatch($chunk);
            $bytes  += $batch['bytes'];
            $absent  = array_merge($absent, $batch['absent']);
        }

        return ['bytes' => $bytes, 'absent' => $absent];
    }

    /**
     * @param string[] $urls  already unique, size <= MAX_URLS_PER_BATCH
     * @return array{bytes: array<string,string>, absent: list<string>}
     */
    private function fetchBatch(array $urls): array
    {
        try {
            $data = $this->http->request('POST', '/fetch', ['json' => ['urls' => $urls]])
                ->toArray();
        } catch (\Throwable $e) {
            throw new \RuntimeException(
                'go-fetcher: batch request failed: '.$e->getMessage(),
                0,
                $e
            );
        }

        $bytes  = [];
        $absent = [];
        foreach ($data['results'] ?? [] as $item) {
            if (!is_array($item) || !isset($item['url'])) {
                continue;
            }
            // Batch policy: an unusable result is dropped, never fatal — one bad
            // image must not sink the whole ingest.
            $body = $this->decodeBody($item);
            if ($body !== null) {
                $bytes[(string) $item['url']] = $body;
            } elseif ($this->isDefinitiveAbsence($item)) {
                $absent[] = (string) $item['url'];
            }
        }

        return ['bytes' => $bytes, 'absent' => $absent];
    }

    /**
     * Same classification as {@see failure()}: a clean upstream 403/404, never
     * a gateway-level error (those are transient by policy).
     *
     * @param array<string,mixed> $item
     */
    private function isDefinitiveAbsence(array $item): bool
    {
        return !isset($item['error'])
            && \in_array((int) ($item['status'] ?? 0), [403, 404], true);
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
        // Single-fetch policy: the caller asked for this exact resource, so an
        // unusable result is an error it must be able to classify.
        return $this->decodeBody($item) ?? throw $this->failure($item, $url);
    }

    /**
     * Usable body bytes of one gateway result, or null when it carries an error,
     * a non-2xx status or an undecodable body.
     *
     * Sole reader of the gateway's response shape (`error`, `status`,
     * `body_base64`): batch and single fetch differ only in what they do with a
     * null, never in how they read it.
     *
     * @param array<string,mixed> $item
     */
    private function decodeBody(array $item): ?string
    {
        $status = (int) ($item['status'] ?? 0);
        if (isset($item['error']) || $status < 200 || $status >= 300) {
            return null;
        }
        $bytes = base64_decode((string) ($item['body_base64'] ?? ''), true);

        return $bytes === false ? null : $bytes;
    }

    /**
     * Classifies why a result was unusable. 403/404 = resource definitively
     * absent (fallback/degradation is fine); any other non-2xx = transient
     * outage, propagated as-is.
     *
     * @param array<string,mixed> $item
     */
    private function failure(array $item, string $url): \RuntimeException
    {
        if (isset($item['error'])) {
            return new \RuntimeException('go-fetcher: '.$item['error']);
        }

        $status = (int) ($item['status'] ?? 0);
        if ($status === 403 || $status === 404) {
            return new UpstreamNotFoundException(
                sprintf('go-fetcher: upstream %d for %s', $status, $url)
            );
        }
        if ($status < 200 || $status >= 300) {
            return new \RuntimeException(
                sprintf('go-fetcher: upstream status %d for %s', $status, $url)
            );
        }

        return new \RuntimeException('go-fetcher: invalid base64 body for '.$url);
    }
}
