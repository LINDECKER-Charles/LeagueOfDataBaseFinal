<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Picker;

use App\Service\Picker\ChampionOptionsProjector;
use PHPUnit\Framework\TestCase;

/**
 * The champion projector realigns the manager's POSITIONAL image list on ids
 * (consuming one slot per entry that has name + image, the manager's own rule)
 * and sorts options by name.
 */
final class ChampionOptionsProjectorTest extends TestCase
{
    private ChampionOptionsProjector $projector;

    protected function setUp(): void
    {
        $this->projector = new ChampionOptionsProjector();
    }

    /** @return array<string, array<string, mixed>> */
    private function data(): array
    {
        return [
            'Zed' => ['id' => 'Zed', 'key' => '238', 'name' => 'Zed', 'image' => ['full' => 'Zed.png']],
            // No image node: the manager emits NO positional slot for this entry.
            'Aatrox' => ['id' => 'Aatrox', 'key' => '266', 'name' => 'Aatrox'],
            'Ahri' => ['id' => 'Ahri', 'key' => '103', 'name' => 'Ahri', 'image' => ['full' => 'Ahri.png']],
        ];
    }

    public function testProjectSortsByNameAndAlignsPositionalImages(): void
    {
        // Two positional slots only (Zed, Ahri) — Aatrox has no image entry.
        $options = $this->projector->project($this->data(), ['cdn/blobs/zed.png', 'cdn/blobs/ahri.png']);

        self::assertSame(['Aatrox', 'Ahri', 'Zed'], array_column($options, 'name'));
        self::assertSame(
            ['Aatrox' => null, 'Ahri' => '/cdn/blobs/ahri.png', 'Zed' => '/cdn/blobs/zed.png'],
            array_column($options, 'image', 'id'),
        );
        self::assertSame('266', array_column($options, 'key', 'id')['Aatrox']);
    }

    public function testUnresolvedImageStaysNull(): void
    {
        // Zed's slot resolved to null (ingestion deferred) — Ahri keeps its path.
        $options = $this->projector->project($this->data(), [null, 'cdn/blobs/ahri.png']);

        self::assertSame(
            ['Aatrox' => null, 'Ahri' => '/cdn/blobs/ahri.png', 'Zed' => null],
            array_column($options, 'image', 'id'),
        );
    }

    public function testResolveFindsChampionWithAlignedImage(): void
    {
        $resolved = $this->projector->resolve($this->data(), ['cdn/blobs/zed.png', 'cdn/blobs/ahri.png'], 'Ahri');

        self::assertSame(
            ['id' => 'Ahri', 'name' => 'Ahri', 'image' => '/cdn/blobs/ahri.png', 'type' => 'champion'],
            $resolved,
        );
    }

    public function testResolveUnknownIdReturnsNull(): void
    {
        self::assertNull($this->projector->resolve($this->data(), [], 'Zzzz'));
    }
}
