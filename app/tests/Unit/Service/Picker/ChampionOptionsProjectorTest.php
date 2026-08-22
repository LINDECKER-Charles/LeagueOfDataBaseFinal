<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Picker;

use App\Service\Picker\ChampionOptionsProjector;
use PHPUnit\Framework\TestCase;

/**
 * The champion projector reads the manager's id-keyed image map (an imageless
 * entry is simply absent from it) and sorts options by name.
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
            'Zed' => [
                'id' => 'Zed',
                'key' => '238',
                'name' => 'Zed',
                'image' => ['full' => 'Zed.png'],
            ],
            // No image node: absent from the manager's id-keyed image map.
            'Aatrox' => ['id' => 'Aatrox', 'key' => '266', 'name' => 'Aatrox'],
            'Ahri' => [
                'id' => 'Ahri',
                'key' => '103',
                'name' => 'Ahri',
                'image' => ['full' => 'Ahri.png'],
            ],
        ];
    }

    public function testProjectSortsByNameAndReadsImagesById(): void
    {
        // Aatrox has no image entry: absent key, null image.
        $options = $this->projector->project(
            $this->data(),
            ['Zed' => 'cdn/blobs/zed.png', 'Ahri' => 'cdn/blobs/ahri.png'],
        );

        self::assertSame(['Aatrox', 'Ahri', 'Zed'], array_column($options, 'name'));
        self::assertSame(
            ['Aatrox' => null, 'Ahri' => '/cdn/blobs/ahri.png', 'Zed' => '/cdn/blobs/zed.png'],
            array_column($options, 'image', 'id'),
        );
        self::assertSame('266', array_column($options, 'key', 'id')['Aatrox']);
    }

    public function testUnresolvedImageStaysNull(): void
    {
        // Zed resolved to null (ingestion deferred) — Ahri keeps its path.
        $options = $this->projector->project(
            $this->data(),
            ['Zed' => null, 'Ahri' => 'cdn/blobs/ahri.png'],
        );

        self::assertSame(
            ['Aatrox' => null, 'Ahri' => '/cdn/blobs/ahri.png', 'Zed' => null],
            array_column($options, 'image', 'id'),
        );
    }

    public function testResolveFindsChampionWithAlignedImage(): void
    {
        $resolved = $this->projector->resolve(
            $this->data(),
            ['Zed' => 'cdn/blobs/zed.png', 'Ahri' => 'cdn/blobs/ahri.png'],
            'Ahri',
        );

        self::assertSame(
            [
                'id' => 'Ahri',
                'name' => 'Ahri',
                'image' => '/cdn/blobs/ahri.png',
                'type' => 'champion',
            ],
            $resolved,
        );
    }

    public function testResolveUnknownIdReturnsNull(): void
    {
        self::assertNull($this->projector->resolve($this->data(), [], 'Zzzz'));
    }
}
