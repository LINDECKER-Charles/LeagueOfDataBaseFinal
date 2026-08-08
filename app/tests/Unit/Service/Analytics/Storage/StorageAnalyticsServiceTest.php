<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics\Storage;

use App\Service\Analytics\Storage\StorageAnalyticsService;
use League\Flysystem\DirectoryAttributes;
use League\Flysystem\DirectoryListing;
use League\Flysystem\FileAttributes;
use League\Flysystem\FilesystemOperator;
use PHPUnit\Framework\TestCase;
use Symfony\Component\Cache\Adapter\ArrayAdapter;

final class StorageAnalyticsServiceTest extends TestCase
{
    private function service(FilesystemOperator $operator): StorageAnalyticsService
    {
        return new StorageAnalyticsService($operator, new ArrayAdapter());
    }

    private function populatedOperator(): FilesystemOperator
    {
        $operator = $this->createMock(FilesystemOperator::class);
        $operator->method('listContents')
            ->with('', FilesystemOperator::LIST_DEEP)
            ->willReturn(new DirectoryListing([
                new FileAttributes('blobs/a.png', 100),
                new FileAttributes('blobs/a.webp', 40),
                new FileAttributes('blobs/b.png', 200),
                new DirectoryAttributes('blobs'),
                new FileAttributes('data/15.1.1/en_US/champion.json', 300),
                new FileAttributes('data/15.1.1/fr_FR/champion.json', 250),
                new FileAttributes('data/15.1.1/en_US/championDetail/Aatrox.json', 80),
                new FileAttributes('manifest/15.1.1/champion.json', 30),
                new FileAttributes('analytics/daily/2026-07-15.json', 50),
            ]));
        $operator->method('read')
            ->willReturn('{"a.png":"cdn/blobs/x.png","b.png":"cdn/blobs/y.png"}');

        return $operator;
    }

    public function testTotalsAndFamilies(): void
    {
        $report = $this->service($this->populatedOperator())->report();

        self::assertTrue($report['ok']);
        self::assertSame(8, $report['total']['objects']); // directory excluded
        $families = array_column($report['families'], 'name');
        self::assertContains('blobs', $families);
        self::assertContains('analytics', $families);
    }

    public function testWebpCoverageAndBlobBreakdown(): void
    {
        $report = $this->service($this->populatedOperator())->report();

        self::assertSame(2, $report['blobs']['sources']); // a.png, b.png
        self::assertSame(1, $report['blobs']['webpSiblings']); // a.webp
        self::assertEqualsWithDelta(0.5, $report['blobs']['webpCoverage'], 0.001);
    }

    public function testDataBreakdownByVersionLangType(): void
    {
        $report = $this->service($this->populatedOperator())->report();

        $byVersion = array_column($report['data']['byVersion'], 'bytes', 'name');
        self::assertSame(630, $byVersion['15.1.1']); // 300 + 250 + 80

        $byLang = array_column($report['data']['byLang'], 'bytes', 'name');
        self::assertSame(380, $byLang['en_US']); // 300 + 80 (championDetail)
        self::assertSame(250, $byLang['fr_FR']);

        $types = array_column($report['data']['byType'], 'name');
        self::assertContains('championDetail', $types);
    }

    public function testDedupCrossReferencesManifests(): void
    {
        $report = $this->service($this->populatedOperator())->report();

        self::assertSame(2, $report['dedup']['logicalRefs']); // 2 manifest entries
        self::assertSame(3, $report['dedup']['physicalBlobs']); // 3 blob files
    }

    public function testCoverageMatrix(): void
    {
        $report = $this->service($this->populatedOperator())->report();

        self::assertCount(1, $report['coverage']);
        self::assertSame('15.1.1', $report['coverage'][0]['version']);
        self::assertSame(['en_US', 'fr_FR'], $report['coverage'][0]['langs']);
    }

    public function testDegradesGracefullyWhenStorageUnavailable(): void
    {
        $operator = $this->createStub(FilesystemOperator::class);
        $operator->method('listContents')->willThrowException(new \RuntimeException('minio down'));

        $report = $this->service($operator)->report();

        self::assertFalse($report['ok']);
        self::assertSame('minio down', $report['error']);
        self::assertSame(['objects' => 0, 'bytes' => 0], $report['total']);
    }

    /**
     * The panel renders the degraded payload with the same templates as the
     * nominal one: a section present in only one of the two shapes would blow up
     * exactly on the error path nobody exercises by hand.
     */
    public function testDegradedReportHasTheSameShapeAsANominalOne(): void
    {
        $broken = $this->createStub(FilesystemOperator::class);
        $broken->method('listContents')->willThrowException(new \RuntimeException('minio down'));

        $degraded = $this->service($broken)->report();
        $nominal = $this->service($this->populatedOperator())->report();

        self::assertSame(array_keys($nominal), array_keys($degraded));
        foreach (['blobs', 'data', 'manifests', 'dedup'] as $section) {
            self::assertSame(
                array_keys($nominal[$section]),
                array_keys($degraded[$section]),
                sprintf('section "%s" diverges between the two report shapes', $section),
            );
        }
    }
}
