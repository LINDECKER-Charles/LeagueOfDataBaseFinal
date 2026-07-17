<?php
declare(strict_types=1);

namespace App\Service\Analytics;

/**
 * One captured page view. Immutable value object persisted as a single NDJSON
 * line by {@see EventStore}; the aggregators reconstruct it from that array.
 *
 * The field set is the contract shared by the writer (terminate listener), the
 * NDJSON line format, and every reader — keep {@see toArray()}/{@see fromArray()}
 * symmetrical. Timestamps are always UTC ISO-8601.
 */
final readonly class RequestEvent
{
    public function __construct(
        public \DateTimeImmutable $at,
        public string $route,
        public string $path,
        public string $type,      // champion|item|runesReforged|summoner|home
        public string $kind,      // home|list|detail
        public ?string $entity,   // detail target (e.g. "Aatrox"), null otherwise
        public int $status,
        public ?string $version,
        public ?string $lang,
        public string $locale,
        public ?string $ip,
        public string $visitorId,
        public ?string $userAgent,
        public string $browser,
        public string $os,
        public string $device,    // desktop|mobile|tablet|bot|other
        public bool $isBot,
        public ?string $refererHost,
        public string $refererSource, // direct|internal|search|social|external
        public ?string $country,      // ISO-3166 alpha-2
        public ?string $countryName,
    ) {}

    /** @return array<string, scalar|null> */
    public function toArray(): array
    {
        return [
            'at' => $this->at->format(\DateTimeInterface::ATOM),
            'route' => $this->route,
            'path' => $this->path,
            'type' => $this->type,
            'kind' => $this->kind,
            'entity' => $this->entity,
            'status' => $this->status,
            'version' => $this->version,
            'lang' => $this->lang,
            'locale' => $this->locale,
            'ip' => $this->ip,
            'visitor' => $this->visitorId,
            'ua' => $this->userAgent,
            'browser' => $this->browser,
            'os' => $this->os,
            'device' => $this->device,
            'bot' => $this->isBot,
            'refHost' => $this->refererHost,
            'refSource' => $this->refererSource,
            'country' => $this->country,
            'countryName' => $this->countryName,
        ];
    }

    /** @param array<string, mixed> $row */
    public static function fromArray(array $row): self
    {
        return new self(
            at: new \DateTimeImmutable((string) ($row['at'] ?? 'now'), new \DateTimeZone('UTC')),
            route: (string) ($row['route'] ?? ''),
            path: (string) ($row['path'] ?? ''),
            type: (string) ($row['type'] ?? ''),
            kind: (string) ($row['kind'] ?? ''),
            entity: self::nullableString($row['entity'] ?? null),
            status: (int) ($row['status'] ?? 0),
            version: self::nullableString($row['version'] ?? null),
            lang: self::nullableString($row['lang'] ?? null),
            locale: (string) ($row['locale'] ?? ''),
            ip: self::nullableString($row['ip'] ?? null),
            visitorId: (string) ($row['visitor'] ?? ''),
            userAgent: self::nullableString($row['ua'] ?? null),
            browser: (string) ($row['browser'] ?? 'other'),
            os: (string) ($row['os'] ?? 'other'),
            device: (string) ($row['device'] ?? 'other'),
            isBot: (bool) ($row['bot'] ?? false),
            refererHost: self::nullableString($row['refHost'] ?? null),
            refererSource: (string) ($row['refSource'] ?? 'direct'),
            country: self::nullableString($row['country'] ?? null),
            countryName: self::nullableString($row['countryName'] ?? null),
        );
    }

    private static function nullableString(mixed $value): ?string
    {
        return $value === null || $value === '' ? null : (string) $value;
    }
}
