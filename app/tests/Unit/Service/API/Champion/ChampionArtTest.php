<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API\Champion;

use App\Service\API\Champion\ChampionArt;
use App\Service\API\Champion\ChampionArtKind;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;

/**
 * Since LoL Classic the CDN serves the public "Fiddlesticks" spelling with the
 * pre-rework art (and 403s the post-rework skins), so Riot's internal spelling
 * must win on EVERY art family — not only the case-strict `centered/` one.
 */
final class ChampionArtTest extends TestCase
{
    private const CDN = 'https://ddragon.leagueoflegends.com/cdn/img/champion';

    private ChampionArt $art;

    protected function setUp(): void
    {
        $this->art = new ChampionArt();
    }

    /** @return iterable<string, array{0: ChampionArtKind}> */
    public static function kinds(): iterable
    {
        foreach (ChampionArtKind::cases() as $kind) {
            yield $kind->value => [$kind];
        }
    }

    #[DataProvider('kinds')]
    public function testFiddlesticksUsesRiotsInternalSpellingOnEveryArtKind(ChampionArtKind $kind): void
    {
        self::assertSame(
            sprintf('%s/%s/FiddleSticks_27.jpg', self::CDN, $kind->value),
            $this->art->url('Fiddlesticks', $kind, 27),
        );
    }

    #[DataProvider('kinds')]
    public function testOtherChampionsKeepTheirPublicId(ChampionArtKind $kind): void
    {
        self::assertSame(
            sprintf('%s/%s/MonkeyKing_0.jpg', self::CDN, $kind->value),
            $this->art->url('MonkeyKing', $kind),
        );
    }

    public function testSkinNumberDefaultsToTheBaseSkin(): void
    {
        self::assertSame(
            self::CDN.'/splash/Ahri_0.jpg',
            $this->art->url('Ahri', ChampionArtKind::Splash),
        );
    }
}
