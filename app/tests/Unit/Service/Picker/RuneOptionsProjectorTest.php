<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Picker;

use App\Service\Picker\RuneOptionsProjector;
use PHPUnit\Framework\TestCase;

/**
 * Rune trees keep DDragon's canonical shape (slots = list of perk lists, order
 * preserved), shortDesc is stripped of markup server-side, and resolution
 * accepts the numeric id of a perk OR of a whole tree.
 */
final class RuneOptionsProjectorTest extends TestCase
{
    private RuneOptionsProjector $projector;

    protected function setUp(): void
    {
        $this->projector = new RuneOptionsProjector();
    }

    /** @return list<array<string, mixed>> */
    private function data(): array
    {
        return [
            [
                'id' => 8100,
                'key' => 'Domination',
                'icon' => 'perk-images/Styles/7200_Domination.png',
                'name' => 'Domination',
                'slots' => [
                    ['runes' => [
                        [
                            'id' => 8112,
                            'key' => 'Electrocute',
                            'icon' => 'perk-images/Styles/Domination/Electrocute/Electrocute.png',
                            'name' => 'Électrocution',
                            'shortDesc' => "Toucher un champion avec 3 attaques <b>distinctes</b> inflige des <lol-uikit-tooltipped-keyword key='LinkTooltip'>dégâts adaptatifs</lol-uikit-tooltipped-keyword> bonus.",
                        ],
                    ]],
                    ['runes' => [
                        ['id' => 8126, 'key' => 'CheapShot', 'icon' => 'x/CheapShot.png', 'name' => 'Coup bas', 'shortDesc' => 'Plain.'],
                    ]],
                ],
            ],
        ];
    }

    /** @return array<string, mixed> */
    private function images(): array
    {
        return [
            'Domination' => [
                'icon' => 'cdn/blobs/domi.png',
                'slots' => [
                    0 => ['Electrocute' => 'cdn/blobs/electro.png'],
                    // Slot 1 unresolved (deferred ingestion) → icons null.
                ],
            ],
        ];
    }

    public function testProjectKeepsSlotShapeAndStripsShortDescMarkup(): void
    {
        $trees = $this->projector->project($this->data(), $this->images());

        self::assertCount(1, $trees);
        $tree = $trees[0];
        self::assertSame([8100, 'Domination', 'Domination', '/cdn/blobs/domi.png'], [$tree['id'], $tree['key'], $tree['name'], $tree['icon']]);
        self::assertCount(2, $tree['slots'], 'slots stay a list of perk lists');

        $electrocute = $tree['slots'][0][0];
        self::assertSame(8112, $electrocute['id']);
        self::assertSame('/cdn/blobs/electro.png', $electrocute['icon']);
        self::assertSame(
            'Toucher un champion avec 3 attaques distinctes inflige des dégâts adaptatifs bonus.',
            $electrocute['shortDesc'],
            'DDragon inline markup stripped, text kept',
        );

        self::assertNull($tree['slots'][1][0]['icon'], 'unresolved slot icon degrades to null');
    }

    public function testResolveAcceptsTreeId(): void
    {
        $resolved = $this->projector->resolve($this->data(), $this->images(), '8100');

        self::assertSame(['id' => '8100', 'name' => 'Domination', 'image' => '/cdn/blobs/domi.png', 'type' => 'rune'], $resolved);
    }

    public function testResolveAcceptsPerkId(): void
    {
        $resolved = $this->projector->resolve($this->data(), $this->images(), '8112');

        self::assertSame(['id' => '8112', 'name' => 'Électrocution', 'image' => '/cdn/blobs/electro.png', 'type' => 'rune'], $resolved);
    }

    public function testResolveUnknownIdReturnsNull(): void
    {
        self::assertNull($this->projector->resolve($this->data(), $this->images(), '9999'));
    }
}
