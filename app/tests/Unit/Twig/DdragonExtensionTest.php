<?php
declare(strict_types=1);

namespace App\Tests\Unit\Twig;

use App\Twig\DdragonExtension;
use PHPUnit\Framework\TestCase;

final class DdragonExtensionTest extends TestCase
{
    private DdragonExtension $extension;

    protected function setUp(): void
    {
        $this->extension = new DdragonExtension();
    }

    public function testStripsUnresolvedCurlyTokens(): void
    {
        $raw = 'Cooldown: {{ Item_Cooldown }} seconds {{ Item_Melee_Ranged_Split }}';

        self::assertSame('Cooldown: seconds', $this->extension->clean($raw));
    }

    public function testStripsAtVarTokens(): void
    {
        self::assertSame('Heals for .', $this->extension->clean('Heals for @BaseHeal@.'));
    }

    public function testPreservesRegularDdragonMarkup(): void
    {
        $html = '<mainText><stats><attention>+40</attention> AD</stats><br><passive>Mist</passive></mainText>';

        self::assertSame($html, $this->extension->clean($html));
    }

    public function testNullAndEmptyYieldEmptyString(): void
    {
        self::assertSame('', $this->extension->clean(null));
        self::assertSame('', $this->extension->clean(''));
    }
}
