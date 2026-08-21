<?php
declare(strict_types=1);

namespace App\Tests\Unit\Twig;

use App\Twig\EditionExtension;
use PHPUnit\Framework\TestCase;

/**
 * The list templates iterate raw Data Dragon maps: item ids arrive as PHP ints
 * (json_decode recasts numeric keys), spells as their full node.
 */
final class EditionExtensionTest extends TestCase
{
    private EditionExtension $extension;

    protected function setUp(): void
    {
        $this->extension = new EditionExtension();
    }

    public function testExposesBothHelpersUnderTheirTemplateNames(): void
    {
        $names = array_map(
            static fn ($function): string => $function->getName(),
            $this->extension->getFunctions(),
        );

        self::assertSame(['item_edition', 'summoner_edition'], $names);
    }

    public function testItemEditionAcceptsTheIntKeysTwigLoopsYield(): void
    {
        self::assertSame('classic', $this->extension->itemEdition(771004));
        self::assertSame('modern', $this->extension->itemEdition('1004'));
    }

    public function testSummonerEditionReadsTheSpellNode(): void
    {
        self::assertSame('classic', $this->extension->summonerEdition(['modes' => ['JADE']]));
        self::assertSame('modern', $this->extension->summonerEdition(['modes' => ['CLASSIC']]));
    }
}
