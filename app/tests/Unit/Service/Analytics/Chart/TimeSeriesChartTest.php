<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics\Chart;

use App\Service\Analytics\Chart\NumberFormat;
use App\Service\Analytics\Chart\SvgPrimitives;
use App\Service\Analytics\Chart\TimeSeriesChart;
use PHPUnit\Framework\TestCase;

final class TimeSeriesChartTest extends TestCase
{
    private const SERIES = [
        ['date' => '2026-07-15', 'views' => 4, 'visitors' => 2],
        ['date' => '2026-07-16', 'views' => 9, 'visitors' => 5],
    ];
    private const LINES = [
        ['key' => 'views', 'label' => 'Vues', 'color' => 'var(--gold)'],
        ['key' => 'visitors', 'label' => 'Visiteurs', 'color' => 'var(--hex)'],
    ];

    private TimeSeriesChart $chart;

    protected function setUp(): void
    {
        $this->chart = new TimeSeriesChart(new SvgPrimitives(), new NumberFormat());
    }

    public function testRendersServerSideMarksAndPerPointTitles(): void
    {
        $html = $this->chart->render(self::SERIES, self::LINES);

        self::assertStringStartsWith('<figure class="chart-fig"', $html);
        self::assertSame(2, substr_count($html, '<polyline'));
        self::assertSame(1, substr_count($html, '<polygon')); // area on the first series only
        self::assertStringContainsString('<title>2026-07-16 — Vues : 9</title>', $html);
    }

    public function testEmptyStateWhenNoData(): void
    {
        self::assertStringContainsString('Aucune donnée', $this->chart->render([], self::LINES));
    }

    public function testMarksAreClippedAndGroupedForTheZoomTransform(): void
    {
        $html = $this->chart->render(self::SERIES, self::LINES);

        self::assertMatchesRegularExpression('/<clipPath id="cp-[0-9a-f]{10}">/', $html);
        self::assertMatchesRegularExpression('/<g class="c-plot" clip-path="url\(#cp-[0-9a-f]{10}\)">/', $html);
    }

    public function testPayloadCarriesRawValuesForTheClientTooltip(): void
    {
        $html = $this->chart->render(self::SERIES, self::LINES);

        $payload = $this->payloadOf($html);
        self::assertSame(['2026-07-15', '2026-07-16'], $payload['dates']);
        // Loose comparison: json_encode drops the zero fraction of whole floats,
        // which is irrelevant to the JavaScript consumer.
        self::assertEquals(9, $payload['max']);
        self::assertSame('Vues', $payload['series'][0]['label']);
        self::assertEquals([4, 9], $payload['series'][0]['values']);
        self::assertEquals([2, 5], $payload['series'][1]['values']);
        self::assertSame('int', $payload['series'][0]['format']);
    }

    public function testPayloadKeepsTheDeclaredValueFormat(): void
    {
        $html = $this->chart->render(
            [['date' => '2026-07-15', 'bytes' => 2048]],
            [['key' => 'bytes', 'label' => 'Poids', 'color' => 'var(--gold)', 'format' => 'bytes']],
        );

        self::assertSame('bytes', $this->payloadOf($html)['series'][0]['format']);
    }

    public function testPayloadIsAttributeEscaped(): void
    {
        $html = $this->chart->render(self::SERIES, [['key' => 'views', 'label' => 'A & "B"', 'color' => 'x']]);

        self::assertStringNotContainsString('data-chart="{"', $html);
        self::assertSame('A & "B"', $this->payloadOf($html)['series'][0]['label']);
    }

    /** @return array<string, mixed> */
    private function payloadOf(string $html): array
    {
        self::assertSame(1, preg_match('/data-chart="([^"]*)"/', $html, $m));

        return json_decode(html_entity_decode($m[1], ENT_QUOTES, 'UTF-8'), true, flags: JSON_THROW_ON_ERROR);
    }
}
