<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Tools;

use App\Service\Tools\DdragonText;
use PHPUnit\Framework\TestCase;

/**
 * The one owner of "clean Data Dragon's leaked copy": every surface (Twig
 * filter, JSON-LD) must strip the same tokens the CDN never resolves.
 */
final class DdragonTextTest extends TestCase
{
    public function testCleanStripsLeakedTokensOnly(): void
    {
        self::assertSame(
            'Slows by and reveals.',
            DdragonText::clean('Slows by @Slow@ and {{ Item_Cooldown }} reveals.'),
        );
        self::assertSame('<stats>+10</stats>', DdragonText::clean('<stats>+10</stats>'));
        self::assertSame('', DdragonText::clean(null));
    }

    public function testPlainNameKeepsTheNameProperOfAMarkedUpField(): void
    {
        self::assertSame(
            'Feu à volonté',
            DdragonText::plainName(
                '<rarityLegendary>Feu à volonté</rarityLegendary>'
                .'<br><subtitleLeft><silver>500 serpents</silver></subtitleLeft>'
            ),
        );
        self::assertSame('Long Sword', DdragonText::plainName('Long Sword'));
        self::assertSame('', DdragonText::plainName(null));
    }
}
