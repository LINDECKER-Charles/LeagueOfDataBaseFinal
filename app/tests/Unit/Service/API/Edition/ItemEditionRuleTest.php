<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API\Edition;

use App\Service\API\Edition\Edition;
use App\Service\API\Edition\ItemEditionRule;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;

/**
 * The classic item catalogue is the "77" six-digit id range and nothing else:
 * not the maps flags (incoherent on classic items, and set on every current
 * item for the Classic Rift), not the other prefixed ranges (Arena "22…").
 */
final class ItemEditionRuleTest extends TestCase
{
    /** @return iterable<string, array{string, Edition}> */
    public static function ids(): iterable
    {
        yield 'current 4-digit'       => ['1004', Edition::Modern];
        yield 'classic twin'          => ['771004', Edition::Classic];
        yield 'classic upgrade'       => ['773070', Edition::Classic];
        yield 'arena variant (22…)'   => ['221011', Edition::Modern];
        yield 'event range (66…)'     => ['664011', Edition::Modern];
        yield 'too short for classic' => ['7710', Edition::Modern];
        yield 'too long for classic'  => ['7710040', Edition::Modern];
    }

    #[DataProvider('ids')]
    public function testTheIdRangeDecidesTheEdition(string $id, Edition $expected): void
    {
        self::assertSame($expected, ItemEditionRule::of($id));
    }

    public function testTheTwinIdIsDerivedBothWaysForTheTwinnableRangesOnly(): void
    {
        self::assertSame('1004', ItemEditionRule::counterpartId('771004'));
        self::assertSame('771004', ItemEditionRule::counterpartId('1004'));
        self::assertNull(ItemEditionRule::counterpartId('221011'), 'Arena variant: no twin');
        self::assertNull(ItemEditionRule::counterpartId('7710040'), 'outside both ranges');
    }

    /**
     * The raw maps flags are honest for neither edition: a classic item belongs
     * to the Classic Rift whatever they claim, a current item keeps its flags
     * minus the 453 every current item carries without existing there.
     */
    public function testClaimableMapsIgnoreTheUntrustworthyFlags(): void
    {
        self::assertSame(
            ['453'],
            ItemEditionRule::claimableMapIds('771004', ['12' => true, '453' => true]),
        );
        self::assertSame(
            ['453'],
            ItemEditionRule::claimableMapIds('771500', ['11' => true, '12' => true]),
        );
        self::assertSame(
            ['11', '12'],
            ItemEditionRule::claimableMapIds(
                '1004',
                ['11' => true, '12' => true, '21' => false, '453' => true],
            ),
        );
        self::assertSame([], ItemEditionRule::claimableMapIds('1004', []));
    }

    public function testOnlyTheClassicTwinIsQualifiedByItsId(): void
    {
        self::assertSame('Faerie Charm', ItemEditionRule::qualifiedName('1004', 'Faerie Charm'));
        self::assertSame(
            'Faerie Charm [771004]',
            ItemEditionRule::qualifiedName('771004', 'Faerie Charm'),
        );
    }
}
