<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Storage;

use App\Service\Storage\BlobStore;
use App\Service\Storage\ImageTranscoder;
use League\Flysystem\Filesystem;
use League\Flysystem\Local\LocalFilesystemAdapter;
use PHPUnit\Framework\TestCase;

final class BlobStoreTest extends TestCase
{
    private string $dir;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir().'/lodb_blob_'.bin2hex(random_bytes(6));
    }

    protected function tearDown(): void
    {
        if (!is_dir($this->dir)) {
            return;
        }
        $it = new \RecursiveIteratorIterator(
            new \RecursiveDirectoryIterator($this->dir, \FilesystemIterator::SKIP_DOTS),
            \RecursiveIteratorIterator::CHILD_FIRST
        );
        foreach ($it as $f) {
            $f->isDir() ? rmdir($f->getPathname()) : unlink($f->getPathname());
        }
        rmdir($this->dir);
    }

    /** @return array{0: BlobStore, 1: Filesystem} */
    private function makeStore(): array
    {
        $fs = new Filesystem(new LocalFilesystemAdapter($this->dir));
        return [new BlobStore($fs, new ImageTranscoder()), $fs];
    }

    private function blobCount(Filesystem $fs): int
    {
        return count(array_filter(
            iterator_to_array($fs->listContents('blobs', true)),
            static fn ($item) => $item->isFile()
        ));
    }

    public function testIdenticalBytesAreStoredOnceAcrossVersions(): void
    {
        [$store, $fs] = $this->makeStore();
        $bytes = str_repeat('A', 2048);

        // Same image content, "different versions" (source name only carries the extension).
        $p1 = $store->store($bytes, '15.1.1/SummonerFlash.png');
        $p2 = $store->store($bytes, '14.9.1/SummonerFlash.png');

        self::assertSame($p1, $p2, 'identical bytes must map to the same content-addressed path');
        self::assertSame(1, $this->blobCount($fs), 'identical bytes must be stored exactly once');
    }

    public function testDifferentBytesAreStoredSeparately(): void
    {
        [$store, $fs] = $this->makeStore();

        $a = $store->store('AAAA', 'a.png');
        $b = $store->store('BBBB', 'b.png');

        self::assertNotSame($a, $b);
        self::assertSame(2, $this->blobCount($fs));
    }

    public function testPathIsSha256ContentAddressed(): void
    {
        [$store] = $this->makeStore();

        $path = $store->store('hello world', 'icon.png');

        self::assertSame('cdn/blobs/'.hash('sha256', 'hello world').'.png', $path);
    }

    public function testExtensionDefaultsToPngWhenMissing(): void
    {
        [$store] = $this->makeStore();

        self::assertStringEndsWith('.png', $store->store('x', 'noext'));
    }
}
