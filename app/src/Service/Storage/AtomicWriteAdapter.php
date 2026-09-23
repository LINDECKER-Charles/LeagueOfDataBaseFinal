<?php
declare(strict_types=1);

namespace App\Service\Storage;

use League\Flysystem\Config;
use League\Flysystem\FileAttributes;
use League\Flysystem\FilesystemAdapter;
use League\Flysystem\FilesystemException;
use League\Flysystem\UnableToWriteFile;

/**
 * Makes every write to the local DDragon storage atomic: bytes land in a hidden
 * staging file first, then a rename() swaps it into place.
 *
 * The object store this replaced gave that for free (a PUT is all-or-nothing).
 * A plain local write truncates then fills the target, so any concurrent reader
 * could see a half-written file: nginx serving a blob — then cached `immutable`
 * for a year —, go-api reading a dataset, or the manifest read-merge-write
 * decoding a truncated JSON and merging into an empty map.
 *
 * Contract: the inner adapter's move() MUST be an atomic rename (the local
 * adapter's is, on a single volume — hence staging inside the storage root).
 */
final class AtomicWriteAdapter implements FilesystemAdapter
{
    private const STAGING_DIR = '.staging';
    private const STAGING_ID_BYTES = 8;

    public function __construct(private readonly FilesystemAdapter $inner) {}

    public function write(string $path, string $contents, Config $config): void
    {
        $this->publish($path, $config, function (string $staging) use ($contents, $config): void {
            $this->inner->write($staging, $contents, $config);
        });
    }

    public function writeStream(string $path, $contents, Config $config): void
    {
        $this->publish($path, $config, function (string $staging) use ($contents, $config): void {
            $this->inner->writeStream($staging, $contents, $config);
        });
    }

    public function copy(string $source, string $destination, Config $config): void
    {
        $stage = function (string $staging) use ($source, $config): void {
            $this->inner->copy($source, $staging, $config);
        };
        $this->publish($destination, $config, $stage);
    }

    public function move(string $source, string $destination, Config $config): void
    {
        $this->inner->move($source, $destination, $config);
    }

    public function fileExists(string $path): bool
    {
        return $this->inner->fileExists($path);
    }

    public function directoryExists(string $path): bool
    {
        return $this->inner->directoryExists($path);
    }

    public function read(string $path): string
    {
        return $this->inner->read($path);
    }

    public function readStream(string $path)
    {
        return $this->inner->readStream($path);
    }

    public function delete(string $path): void
    {
        $this->inner->delete($path);
    }

    public function deleteDirectory(string $path): void
    {
        $this->inner->deleteDirectory($path);
    }

    public function createDirectory(string $path, Config $config): void
    {
        $this->inner->createDirectory($path, $config);
    }

    public function setVisibility(string $path, string $visibility): void
    {
        $this->inner->setVisibility($path, $visibility);
    }

    public function visibility(string $path): FileAttributes
    {
        return $this->inner->visibility($path);
    }

    public function mimeType(string $path): FileAttributes
    {
        return $this->inner->mimeType($path);
    }

    public function lastModified(string $path): FileAttributes
    {
        return $this->inner->lastModified($path);
    }

    public function fileSize(string $path): FileAttributes
    {
        return $this->inner->fileSize($path);
    }

    public function listContents(string $path, bool $deep): iterable
    {
        return $this->inner->listContents($path, $deep);
    }

    /**
     * @param callable(string): void $stage writes the final bytes at the given staging path
     */
    private function publish(string $path, Config $config, callable $stage): void
    {
        $id = bin2hex(random_bytes(self::STAGING_ID_BYTES));
        $staging = self::STAGING_DIR.'/'.$id;
        try {
            $stage($staging);
            $this->inner->move($staging, $path, $config);
        } catch (FilesystemException $e) {
            $this->discard($staging);
            // Report the key the caller asked for, never the internal staging path.
            throw UnableToWriteFile::atLocation($path, $e->getMessage(), $e);
        }
    }

    private function discard(string $staging): void
    {
        try {
            $this->inner->delete($staging);
        } catch (FilesystemException) {
            // Best-effort: an orphan in .staging/ is invisible to every reader.
        }
    }
}
