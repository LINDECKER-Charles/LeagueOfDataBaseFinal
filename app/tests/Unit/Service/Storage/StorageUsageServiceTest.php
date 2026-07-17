<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Storage;

use App\Service\Storage\StorageUsageService;
use League\Flysystem\DirectoryAttributes;
use League\Flysystem\DirectoryListing;
use League\Flysystem\FileAttributes;
use League\Flysystem\FilesystemOperator;
use PHPUnit\Framework\TestCase;

final class StorageUsageServiceTest extends TestCase
{
    public function testAggregatesBytesAndObjectsByTopLevelPrefix(): void
    {
        $operator = $this->createMock(FilesystemOperator::class);
        $operator->expects(self::once())
            ->method('listContents')
            ->with('', FilesystemOperator::LIST_DEEP)
            ->willReturn(new DirectoryListing([
                new FileAttributes('blobs/a.png', 100),
                new FileAttributes('blobs/b.png', 50),
                new DirectoryAttributes('blobs'), // directories must be ignored
                new FileAttributes('data/en_US/champion.json', 300),
                new FileAttributes('manifest/15.1.1.json', 30),
            ]));

        $report = (new StorageUsageService($operator))->report();

        self::assertTrue($report['ok']);
        self::assertNull($report['error']);
        self::assertSame(4, $report['total']['objects']);
        self::assertSame(480, $report['total']['bytes']);
        self::assertSame(['objects' => 2, 'bytes' => 150], $report['prefixes']['blobs']);
        self::assertSame(['objects' => 1, 'bytes' => 300], $report['prefixes']['data']);
        self::assertSame(['objects' => 1, 'bytes' => 30], $report['prefixes']['manifest']);
    }

    public function testPrefixesAreSortedByWeightDescending(): void
    {
        $operator = $this->createStub(FilesystemOperator::class);
        $operator->method('listContents')->willReturn(new DirectoryListing([
            new FileAttributes('manifest/m.json', 10),
            new FileAttributes('blobs/a.png', 1000),
            new FileAttributes('data/d.json', 200),
        ]));

        $report = (new StorageUsageService($operator))->report();

        self::assertSame(['blobs', 'data', 'manifest'], array_keys($report['prefixes']));
    }

    public function testTreatsNullFileSizeAsZero(): void
    {
        $operator = $this->createStub(FilesystemOperator::class);
        $operator->method('listContents')->willReturn(new DirectoryListing([
            new FileAttributes('blobs/x.png', null),
        ]));

        $report = (new StorageUsageService($operator))->report();

        self::assertSame(1, $report['total']['objects']);
        self::assertSame(0, $report['total']['bytes']);
    }

    public function testDegradesGracefullyWhenStorageUnavailable(): void
    {
        $operator = $this->createStub(FilesystemOperator::class);
        $operator->method('listContents')->willThrowException(new \RuntimeException('connection refused'));

        $report = (new StorageUsageService($operator))->report();

        self::assertFalse($report['ok']);
        self::assertSame('connection refused', $report['error']);
        self::assertSame(['objects' => 0, 'bytes' => 0], $report['total']);
        self::assertSame([], $report['prefixes']);
    }
}
