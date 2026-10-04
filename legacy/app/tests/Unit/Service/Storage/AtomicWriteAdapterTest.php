<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Storage;

use App\Service\Storage\AtomicWriteAdapter;
use League\Flysystem\Filesystem;
use League\Flysystem\Local\LocalFilesystemAdapter;
use League\Flysystem\UnableToWriteFile;
use PHPUnit\Framework\TestCase;

/**
 * The storage is read concurrently by nginx, go-api and other PHP requests while
 * php writes it: a reader must see the old bytes or the new ones, never a mix.
 */
final class AtomicWriteAdapterTest extends TestCase
{
    private string $dir;
    private Filesystem $storage;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir().'/lodb_atomic_'.bin2hex(random_bytes(6));
        $this->storage = new Filesystem(
            new AtomicWriteAdapter(new LocalFilesystemAdapter($this->dir)),
            ['visibility' => 'public', 'directory_visibility' => 'public'],
        );
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

    public function testWritesLandAtTheirKeyWithoutStagingResidue(): void
    {
        $this->storage->write('data/15.1.1/fr_FR/champion.json', '{"a":1}');
        $this->storage->writeStream('blobs/abc.png', $this->stream('png-bytes'));

        self::assertSame('{"a":1}', $this->storage->read('data/15.1.1/fr_FR/champion.json'));
        self::assertSame('png-bytes', $this->storage->read('blobs/abc.png'));
        self::assertSame([], $this->stagingFiles());
    }

    public function testOverwriteNeverTruncatesTheFileAReaderHoldsOpen(): void
    {
        $this->storage->write('manifest/15.1.1/item.json', '{"old":true}');
        $reader = fopen($this->dir.'/manifest/15.1.1/item.json', 'rb');
        self::assertIsResource($reader);

        $this->storage->write('manifest/15.1.1/item.json', '{"new":true}');

        // An in-place write would have truncated the open file under the reader.
        self::assertSame('{"old":true}', stream_get_contents($reader));
        fclose($reader);
        self::assertSame('{"new":true}', $this->storage->read('manifest/15.1.1/item.json'));
    }

    public function testFailedPublicationRemovesItsStagingFile(): void
    {
        // A directory already sits at the target key: the final rename must fail.
        $this->storage->write('blobs/taken/inner.png', 'x');

        try {
            $this->storage->write('blobs/taken', 'y');
            self::fail('Writing over a directory must fail.');
        } catch (UnableToWriteFile $e) {
            self::assertSame('blobs/taken', $e->location());
            self::assertSame([], $this->stagingFiles());
        }
    }

    /** @return list<string> */
    private function stagingFiles(): array
    {
        $staging = $this->dir.'/.staging';

        return is_dir($staging) ? array_values(array_diff(scandir($staging), ['.', '..'])) : [];
    }

    /** @return resource */
    private function stream(string $contents)
    {
        $stream = fopen('php://memory', 'r+b');
        fwrite($stream, $contents);
        rewind($stream);

        return $stream;
    }
}
