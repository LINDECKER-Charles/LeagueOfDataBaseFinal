<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API\Edition;

use App\Service\API\Edition\Edition;
use App\Service\API\Edition\SummonerEditionRule;
use PHPUnit\Framework\TestCase;

/**
 * A classic spell is the one playable in the JADE mode; the "_Jade" id suffix
 * only serves to derive the twin.
 */
final class SummonerEditionRuleTest extends TestCase
{
    public function testTheModesListDecidesTheEditionNotTheId(): void
    {
        self::assertSame(
            Edition::Classic,
            SummonerEditionRule::of(['id' => 'SummonerFlash_Jade', 'modes' => ['JADE']]),
        );
        self::assertSame(
            Edition::Modern,
            SummonerEditionRule::of(['id' => 'SummonerFlash', 'modes' => ['CLASSIC', 'ARAM']]),
        );
        self::assertSame(
            Edition::Modern,
            SummonerEditionRule::of(['id' => 'SummonerFlash_Jade']),
            'no modes at all reads as current game — the safe default',
        );
    }

    public function testTheTwinIdTogglesTheJadeSuffix(): void
    {
        self::assertSame('SummonerFlash', SummonerEditionRule::counterpartId('SummonerFlash_Jade'));
        self::assertSame('SummonerFlash_Jade', SummonerEditionRule::counterpartId('SummonerFlash'));
    }
}
