<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Changelog;

use App\Service\Changelog\ChangelogReader;
use PHPUnit\Framework\TestCase;

/**
 * The reader resolves manifest ordering to full release payloads and never
 * throws: a missing manifest, a missing release file, or corrupt JSON each
 * degrade to an empty/partial history rather than an error on a cosmetic page.
 */
final class ChangelogReaderTest extends TestCase
{
    private string $projectDir;

    protected function setUp(): void
    {
        $this->projectDir = sys_get_temp_dir() . '/cl_' . bin2hex(random_bytes(6));
        mkdir($this->projectDir . '/public/changelog', 0777, true);
    }

    protected function tearDown(): void
    {
        $dir = $this->projectDir . '/public/changelog';
        foreach (glob($dir . '/*') ?: [] as $file) {
            unlink($file);
        }
        @rmdir($dir);
        @rmdir($this->projectDir . '/public');
        @rmdir($this->projectDir);
    }

    public function testReturnsReleasesInManifestOrder(): void
    {
        $this->writeManifest([['id' => 'b'], ['id' => 'a']]);
        $this->writeRelease('a', ['version' => '1.0.0']);
        $this->writeRelease('b', ['version' => '2.0.0']);

        $versions = array_column($this->reader()->releases(), 'version');

        // Manifest order is authoritative, not filesystem/lexical order.
        self::assertSame(['2.0.0', '1.0.0'], $versions);
    }

    public function testSkipsManifestEntryWithMissingReleaseFile(): void
    {
        $this->writeManifest([['id' => 'present'], ['id' => 'ghost']]);
        $this->writeRelease('present', ['version' => '1.0.0']);

        $releases = $this->reader()->releases();

        self::assertCount(1, $releases);
        self::assertSame('1.0.0', $releases[0]['version']);
    }

    public function testMissingManifestYieldsEmptyHistory(): void
    {
        self::assertSame([], $this->reader()->releases());
        self::assertSame([], $this->reader()->manifest());
    }

    public function testCorruptManifestYieldsEmptyHistory(): void
    {
        file_put_contents($this->projectDir . '/public/changelog/manifest.json', '{ not json');

        self::assertSame([], $this->reader()->manifest());
    }

    public function testCorruptReleaseFileIsSkipped(): void
    {
        $this->writeManifest([['id' => 'broken'], ['id' => 'ok']]);
        file_put_contents($this->projectDir . '/public/changelog/broken.json', 'nope');
        $this->writeRelease('ok', ['version' => '1.0.0']);

        $releases = $this->reader()->releases();

        self::assertCount(1, $releases);
        self::assertSame('1.0.0', $releases[0]['version']);
    }

    private function reader(): ChangelogReader
    {
        return new ChangelogReader($this->projectDir);
    }

    /** @param list<array<string, mixed>> $patches */
    private function writeManifest(array $patches): void
    {
        file_put_contents(
            $this->projectDir . '/public/changelog/manifest.json',
            json_encode(['patches' => $patches], JSON_THROW_ON_ERROR),
        );
    }

    /** @param array<string, mixed> $payload */
    private function writeRelease(string $id, array $payload): void
    {
        file_put_contents(
            $this->projectDir . '/public/changelog/' . $id . '.json',
            json_encode($payload + ['id' => $id], JSON_THROW_ON_ERROR),
        );
    }
}
