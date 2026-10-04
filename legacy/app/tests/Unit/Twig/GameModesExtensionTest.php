<?php
declare(strict_types=1);

namespace App\Tests\Unit\Twig;

use App\Twig\GameModesExtension;
use PHPUnit\Framework\TestCase;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * The single curated whitelist behind every game-mode chip: internal dev ids
 * are dropped, JADE reads as the translated LoL Classic label, and the summoner
 * list and detail can no longer diverge (they used to keep two copies).
 */
final class GameModesExtensionTest extends TestCase
{
    private GameModesExtension $extension;

    protected function setUp(): void
    {
        $translator = $this->createStub(TranslatorInterface::class);
        $translator->method('trans')->willReturnCallback(
            static fn (string $key): string => $key === 'edition.classic' ? 'LoL Classic' : $key,
        );
        $this->extension = new GameModesExtension($translator);
    }

    public function testLabelsCurateAndTranslateInInputOrder(): void
    {
        self::assertSame(
            ["Summoner's Rift", 'ARAM', 'LoL Classic', 'Legend of the Poro King'],
            $this->extension->labels([
                'CLASSIC', 'WIPMODEWIP', 'ARAM', 'RUBY_TRIAL_1',
                'JADE', 'TUTORIAL_MODULE_1', 'KINGPORO', 'ARAM',
            ]),
        );
    }

    public function testAnAllInternalListYieldsNothingToDisplay(): void
    {
        self::assertSame([], $this->extension->labels(['WIPMODEWIP', 'KIWI', 'RUBY']));
    }
}
