<?php
declare(strict_types=1);

namespace App\Tests\Unit\Command;

use App\Command\GenerateWebpVariantsCommand;
use App\Service\Storage\ImageTranscoder;
use League\Flysystem\Filesystem;
use League\Flysystem\Local\LocalFilesystemAdapter;
use PHPUnit\Framework\TestCase;
use Symfony\Component\Console\Command\Command;
use Symfony\Component\Console\Tester\CommandTester;

/**
 * WebP backfill over a real filesystem: which blobs are picked up, which are
 * left alone, and how the tally maps onto the exit code.
 */
final class GenerateWebpVariantsCommandTest extends TestCase
{
    private string $root;
    private Filesystem $storage;
    private CommandTester $tester;

    protected function setUp(): void
    {
        $transcoder = new ImageTranscoder();
        if (!$transcoder->isSupported()) {
            self::markTestSkipped('GD/WebP unavailable (imagewebp).');
        }

        $this->root = sys_get_temp_dir() . '/webp-backfill-' . bin2hex(random_bytes(6));
        $this->storage = new Filesystem(new LocalFilesystemAdapter($this->root));
        $this->tester = new CommandTester(
            new GenerateWebpVariantsCommand($this->storage, $transcoder),
        );
    }

    protected function tearDown(): void
    {
        if (isset($this->root) && is_dir($this->root)) {
            $this->storage->deleteDirectory('');
            @rmdir($this->root);
        }
    }

    public function testGeneratesSiblingOnlyForRasterSources(): void
    {
        $this->storage->write('blobs/aaa.png', $this->pngBytes());
        $this->storage->write('blobs/bbb.svg', '<svg xmlns="http://www.w3.org/2000/svg"/>');
        $this->storage->write('blobs/ccc.webp', 'already a sibling');

        self::assertSame(Command::SUCCESS, $this->tester->execute([]));
        self::assertTrue($this->storage->fileExists('blobs/aaa.webp'));
        self::assertFalse($this->storage->fileExists('blobs/bbb.webp'));
        self::assertStringContainsString('1 générés', $this->tester->getDisplay());
    }

    public function testExistingSiblingIsSkippedUnlessForced(): void
    {
        $this->storage->write('blobs/aaa.png', $this->pngBytes());
        $this->storage->write('blobs/aaa.webp', 'stale');

        self::assertSame(Command::SUCCESS, $this->tester->execute([]));
        self::assertSame('stale', $this->storage->read('blobs/aaa.webp'));
        self::assertStringContainsString('1 déjà présents', $this->tester->getDisplay());

        self::assertSame(Command::SUCCESS, $this->tester->execute(['--force' => true]));
        self::assertNotSame('stale', $this->storage->read('blobs/aaa.webp'));
    }

    public function testUndecodableSourceCountsAsFailureAndFailsTheCommand(): void
    {
        $this->storage->write('blobs/broken.png', 'not an image');

        self::assertSame(Command::FAILURE, $this->tester->execute([]));
        self::assertFalse($this->storage->fileExists('blobs/broken.webp'));
        self::assertStringContainsString('1 échecs', $this->tester->getDisplay());
    }

    private function pngBytes(): string
    {
        $image = imagecreatetruecolor(2, 2);
        self::assertInstanceOf(\GdImage::class, $image);

        ob_start();
        imagepng($image);
        return (string) ob_get_clean();
    }
}
