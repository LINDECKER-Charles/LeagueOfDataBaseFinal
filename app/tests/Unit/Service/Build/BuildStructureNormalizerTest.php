<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Build;

use App\Service\Build\BuildStructureNormalizer;
use PHPUnit\Framework\TestCase;

/**
 * The normalizer turns a validated (possibly JSON-round-tripped) structure into
 * the exact persisted shape: int perk ids, trimmed labels, nulled blank notes,
 * string item ids.
 */
final class BuildStructureNormalizerTest extends TestCase
{
    private BuildStructureNormalizer $normalizer;

    protected function setUp(): void
    {
        $this->normalizer = new BuildStructureNormalizer();
    }

    public function testCanonicalizesEveryField(): void
    {
        $normalized = $this->normalizer->normalize([
            'championId' => '  Aatrox  ',
            'runes' => [
                'primaryStyleId' => '8000',
                'primarySelections' => ['8005', 9101, '9104', 8014],
                'secondaryStyleId' => 8100,
                'secondarySelections' => ['8126', '8138'],
            ],
            'steps' => [
                ['label' => '  Start  ', 'note' => '   ', 'items' => [1055, '2003']],
                ['label' => 'Core', 'note' => ' rush it ', 'items' => ['3006']],
            ],
        ]);

        self::assertSame([
            'championId' => 'Aatrox',
            'runes' => [
                'primaryStyleId' => 8000,
                'primarySelections' => [8005, 9101, 9104, 8014],
                'secondaryStyleId' => 8100,
                'secondarySelections' => [8126, 8138],
            ],
            'steps' => [
                ['label' => 'Start', 'note' => null, 'items' => ['1055', '2003']],
                ['label' => 'Core', 'note' => 'rush it', 'items' => ['3006']],
            ],
        ], $normalized);
    }

    public function testMalformedInputDegradesToEmptyShapes(): void
    {
        $normalized = $this->normalizer->normalize(['runes' => 'nope', 'steps' => 'nope']);

        self::assertSame('', $normalized['championId']);
        self::assertSame(
            ['primaryStyleId' => 0, 'primarySelections' => [], 'secondaryStyleId' => 0, 'secondarySelections' => []],
            $normalized['runes'],
        );
        self::assertSame([], $normalized['steps']);
    }
}
