<?php
declare(strict_types=1);

namespace App\Service\Analytics;

/**
 * Merges a set of per-day aggregates ({@see AnalyticsAggregator}) into the final
 * traffic + audience report a dashboard renders. Counts sum; visitor sets union
 * (so range uniques are exact, not a sum of daily uniques); a visitor seen on two
 * or more distinct days counts as returning. Pure — no I/O, no framework.
 */
final class RangeReportBuilder
{
    private const TOP_PAGES = 20;
    private const TOP_ENTITIES = 20;
    private const TOP_REFERERS = 15;

    /**
     * @param list<array<string, mixed>> $dailies chronological per-day aggregates
     * @return array<string, mixed>
     */
    public function build(array $dailies, string $range): array
    {
        $maps = $this->mergeAllMaps($dailies);
        $visitors = $this->visitors($dailies);
        $hourly = $this->sumVectors($dailies, 'byHour', 24);
        $weekly = $this->sumVectors($dailies, 'byWeekday', 7);

        return [
            'range' => $range,
            'from' => $dailies[0]['date'] ?? null,
            'to' => $dailies[count($dailies) - 1]['date'] ?? null,
            'days' => count($dailies),
            'totals' => [
                'views' => array_sum(array_column($dailies, 'views')),
                'botViews' => array_sum(array_column($dailies, 'botViews')),
                'uniqueVisitors' => $visitors['unique'],
                'returningVisitors' => $visitors['returning'],
                'pagesTracked' => count($maps['pages']),
            ],
            'series' => $this->series($dailies),
            'byType' => $this->rank($maps['byType']),
            'byKind' => $this->rank($maps['byKind']),
            'byRoute' => $this->rank($maps['byRoute']),
            'status' => $this->rank($maps['status']),
            'topPages' => $this->rank($maps['pages'], self::TOP_PAGES),
            'topEntities' => $this->rank($maps['entities'], self::TOP_ENTITIES),
            'byHour' => $hourly,
            'byWeekday' => $weekly,
            'heatmap' => $this->heatmap($maps['heatmap']),
            'locale' => $this->rank($maps['locale']),
            'lang' => $this->rank($maps['lang']),
            'browser' => $this->rank($maps['browser']),
            'os' => $this->rank($maps['os']),
            'device' => $this->rank($maps['device']),
            'refSource' => $this->rank($maps['refSource']),
            'topReferers' => $this->rank($maps['refHost'], self::TOP_REFERERS),
            'country' => $this->countries($maps['country'], $maps['countryNames']),
        ];
    }

    /**
     * @param list<array<string, mixed>> $dailies
     * @return array<string, array<string, mixed>>
     */
    private function mergeAllMaps(array $dailies): array
    {
        $keys = ['byType', 'byKind', 'byRoute', 'status', 'pages', 'entities',
            'heatmap', 'locale', 'lang', 'browser', 'os', 'device', 'refSource', 'refHost', 'country'];
        $merged = array_fill_keys($keys, []);
        $merged['countryNames'] = [];

        foreach ($dailies as $daily) {
            foreach ($keys as $key) {
                foreach ((array) ($daily[$key] ?? []) as $name => $count) {
                    $merged[$key][$name] = ($merged[$key][$name] ?? 0) + $count;
                }
            }
            $merged['countryNames'] += (array) ($daily['countryNames'] ?? []);
        }

        return $merged;
    }

    /**
     * @param list<array<string, mixed>> $dailies
     * @return array{unique: int, returning: int}
     */
    private function visitors(array $dailies): array
    {
        $days = [];
        foreach ($dailies as $daily) {
            foreach ((array) ($daily['visitors'] ?? []) as $vid) {
                $days[(string) $vid] = ($days[(string) $vid] ?? 0) + 1;
            }
        }
        $returning = 0;
        foreach ($days as $count) {
            if ($count >= 2) {
                $returning++;
            }
        }

        return ['unique' => count($days), 'returning' => $returning];
    }

    /**
     * @param list<array<string, mixed>> $dailies
     * @return list<array{date: string, views: int, visitors: int, botViews: int}>
     */
    private function series(array $dailies): array
    {
        return array_map(static fn (array $d): array => [
            'date' => (string) $d['date'],
            'views' => (int) ($d['views'] ?? 0),
            'visitors' => count((array) ($d['visitors'] ?? [])),
            'botViews' => (int) ($d['botViews'] ?? 0),
        ], $dailies);
    }

    /**
     * @param list<array<string, mixed>> $dailies
     * @return list<int>
     */
    private function sumVectors(array $dailies, string $key, int $size): array
    {
        $out = array_fill(0, $size, 0);
        foreach ($dailies as $daily) {
            foreach ((array) ($daily[$key] ?? []) as $i => $v) {
                if (isset($out[$i])) {
                    $out[$i] += (int) $v;
                }
            }
        }

        return $out;
    }

    /**
     * @param array<string, int> $heatmap "weekday:hour" => count
     * @return list<list<int>> 7 rows (Mon..Sun) × 24 hours
     */
    private function heatmap(array $heatmap): array
    {
        $grid = array_map(static fn (): array => array_fill(0, 24, 0), range(0, 6));
        foreach ($heatmap as $cell => $count) {
            [$weekday, $hour] = array_map('intval', explode(':', (string) $cell));
            if (isset($grid[$weekday][$hour])) {
                $grid[$weekday][$hour] = (int) $count;
            }
        }

        return $grid;
    }

    /**
     * @param array<string, int> $codes  ISO code => count
     * @param array<string, string> $names ISO code => display name
     * @return list<array{name: string, code: string, count: int, pct: float}>
     */
    private function countries(array $codes, array $names): array
    {
        $total = array_sum($codes);
        arsort($codes);
        $rows = [];
        foreach ($codes as $code => $count) {
            $rows[] = [
                'name' => $names[$code] ?? (string) $code,
                'code' => (string) $code,
                'count' => $count,
                'pct' => $total > 0 ? $count / $total * 100 : 0.0,
            ];
        }

        return $rows;
    }

    /**
     * @param array<string, int> $map
     * @return list<array{name: string, count: int, pct: float}>
     */
    private function rank(array $map, ?int $limit = null): array
    {
        $total = array_sum($map);
        arsort($map);
        if ($limit !== null) {
            $map = array_slice($map, 0, $limit, true);
        }
        $rows = [];
        foreach ($map as $name => $count) {
            $rows[] = ['name' => (string) $name, 'count' => $count, 'pct' => $total > 0 ? $count / $total * 100 : 0.0];
        }

        return $rows;
    }
}
