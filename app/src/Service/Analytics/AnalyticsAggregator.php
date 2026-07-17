<?php
declare(strict_types=1);

namespace App\Service\Analytics;

/**
 * Folds raw NDJSON events into a per-day aggregate — the mergeable unit shared by
 * the live reader and the MinIO rollup. Pure and framework-free: it takes decoded
 * event rows and returns plain arrays, so both the rollup command and the report
 * service (over local files) reuse the exact same counting logic.
 *
 * Bots are counted apart (botViews) and excluded from every human breakdown so
 * "most consulted pages" and audience metrics reflect real visitors.
 */
final class AnalyticsAggregator
{
    /**
     * @param iterable<array<string, mixed>> $events
     * @return array<string, mixed> a daily aggregate (see emptyDaily())
     */
    public function aggregateDay(string $date, iterable $events): array
    {
        $daily = $this->emptyDaily($date);
        foreach ($events as $event) {
            $this->fold($daily, $event);
        }
        // Persist the visitor set as a compact list.
        $daily['visitors'] = array_keys($daily['visitors']);

        return $daily;
    }

    /**
     * @param array<string, mixed> $daily
     * @param array<string, mixed> $event
     */
    private function fold(array &$daily, array $event): void
    {
        if (!empty($event['bot'])) {
            $daily['botViews']++;

            return;
        }

        $daily['views']++;
        $daily['visitors'][(string) ($event['visitor'] ?? '')] = true;
        $this->foldContent($daily, $event);
        $this->foldAudience($daily, $event);
        $this->foldTime($daily, (string) ($event['at'] ?? ''));
    }

    /**
     * @param array<string, mixed> $daily
     * @param array<string, mixed> $event
     */
    private function foldContent(array &$daily, array $event): void
    {
        $this->inc($daily['byType'], (string) ($event['type'] ?? '?'));
        $this->inc($daily['byKind'], (string) ($event['kind'] ?? '?'));
        $this->inc($daily['byRoute'], (string) ($event['route'] ?? '?'));
        $this->inc($daily['status'], (string) ($event['status'] ?? '?'));

        $path = (string) ($event['path'] ?? '');
        if ($path !== '') {
            $this->inc($daily['pages'], $path);
        }
        $entity = (string) ($event['entity'] ?? '');
        if ($entity !== '') {
            $this->inc($daily['entities'], ($event['type'] ?? '?') . ':' . $entity);
        }
    }

    /**
     * @param array<string, mixed> $daily
     * @param array<string, mixed> $event
     */
    private function foldAudience(array &$daily, array $event): void
    {
        $this->inc($daily['locale'], (string) ($event['locale'] ?? '?'));
        $this->inc($daily['browser'], (string) ($event['browser'] ?? 'other'));
        $this->inc($daily['os'], (string) ($event['os'] ?? 'other'));
        $this->inc($daily['device'], (string) ($event['device'] ?? 'other'));
        $this->inc($daily['refSource'], (string) ($event['refSource'] ?? 'direct'));

        foreach (['lang' => 'lang', 'refHost' => 'refHost'] as $field => $bucket) {
            $value = (string) ($event[$field] ?? '');
            if ($value !== '') {
                $this->inc($daily[$bucket], $value);
            }
        }

        $country = (string) ($event['country'] ?? '');
        if ($country !== '') {
            $this->inc($daily['country'], $country);
            $daily['countryNames'][$country] = (string) ($event['countryName'] ?? $country);
        }
    }

    /**
     * @param array<string, mixed> $daily
     */
    private function foldTime(array &$daily, string $at): void
    {
        if ($at === '') {
            return;
        }
        try {
            $moment = new \DateTimeImmutable($at);
        } catch (\Throwable) {
            return;
        }
        $hour = (int) $moment->format('G');
        $weekday = (int) $moment->format('N') - 1; // 0 = Monday
        $daily['byHour'][$hour]++;
        $daily['byWeekday'][$weekday]++;
        $this->inc($daily['heatmap'], $weekday . ':' . $hour);
    }

    /**
     * @param array<string, int> $map
     */
    private function inc(array &$map, string $key, int $by = 1): void
    {
        $map[$key] = ($map[$key] ?? 0) + $by;
    }

    /**
     * @return array<string, mixed>
     */
    public function emptyDaily(string $date): array
    {
        return [
            'date' => $date,
            'views' => 0, 'botViews' => 0,
            'visitors' => [],
            'byType' => [], 'byKind' => [], 'byRoute' => [], 'status' => [],
            'pages' => [], 'entities' => [],
            'byHour' => array_fill(0, 24, 0), 'byWeekday' => array_fill(0, 7, 0), 'heatmap' => [],
            'locale' => [], 'lang' => [], 'browser' => [], 'os' => [], 'device' => [],
            'refSource' => [], 'refHost' => [], 'country' => [], 'countryNames' => [],
        ];
    }
}
