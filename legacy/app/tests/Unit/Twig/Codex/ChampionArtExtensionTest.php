<?php
declare(strict_types=1);

namespace App\Tests\Unit\Twig\Codex;

use App\Service\API\Champion\ChampionArt;
use App\Twig\Codex\ChampionArtExtension;
use PHPUnit\Framework\TestCase;

final class ChampionArtExtensionTest extends TestCase
{
    private ChampionArtExtension $extension;

    protected function setUp(): void
    {
        $this->extension = new ChampionArtExtension(new ChampionArt());
    }

    public function testExposesChampionArtUnderItsTemplateName(): void
    {
        self::assertSame('champion_art', $this->extension->getFunctions()[0]->getName());
    }

    public function testBuildsTheRequestedKindWithTheInternalSpelling(): void
    {
        self::assertSame(
            'https://ddragon.leagueoflegends.com/cdn/img/champion/loading/FiddleSticks_0.jpg',
            $this->extension->url('Fiddlesticks', 'loading'),
        );
        self::assertSame(
            'https://ddragon.leagueoflegends.com/cdn/img/champion/splash/Ahri_7.jpg',
            $this->extension->url('Ahri', 'splash', 7),
        );
    }

    public function testRejectsAnUnknownArtKind(): void
    {
        $this->expectException(\ValueError::class);

        $this->extension->url('Ahri', 'portrait');
    }
}
