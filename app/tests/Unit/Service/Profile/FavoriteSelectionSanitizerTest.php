<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Profile;

use App\Service\Profile\FavoriteSelectionSanitizer;
use App\Service\Profile\FavoriteSlot;
use PHPUnit\Framework\TestCase;

/**
 * Sanitize policy: empty clears silently, oversized/unknown ids drop to null
 * WITH the slot flagged (per-slot warning upstream), valid ids pass through,
 * and an unchanged favorite that is merely off the viewed patch is preserved.
 */
final class FavoriteSelectionSanitizerTest extends TestCase
{
    private FavoriteSelectionSanitizer $sanitizer;

    protected function setUp(): void
    {
        $this->sanitizer = new FavoriteSelectionSanitizer();
    }

    public function testValidIdsPassThroughTrimmed(): void
    {
        $result = $this->sanitizer->sanitize(
            ['champion' => '  Aatrox ', 'item' => '3006', 'rune' => '8112', 'summoner' => 'SummonerFlash'],
            [],
            static fn (): bool => true,
        );

        self::assertSame(
            ['champion' => 'Aatrox', 'item' => '3006', 'rune' => '8112', 'summoner' => 'SummonerFlash'],
            $result['values'],
        );
        self::assertSame([], $result['invalid']);
    }

    public function testEmptyOrMissingClearsWithoutWarning(): void
    {
        $result = $this->sanitizer->sanitize(['champion' => '', 'item' => null], [], static fn (): bool => true);

        self::assertSame(
            ['champion' => null, 'item' => null, 'rune' => null, 'summoner' => null],
            $result['values'],
        );
        self::assertSame([], $result['invalid']);
    }

    public function testUnknownIdIsDroppedAndSlotFlagged(): void
    {
        $result = $this->sanitizer->sanitize(
            ['champion' => 'Zzzz', 'item' => '3006'],
            [],
            static fn (FavoriteSlot $slot, string $id): bool => $id !== 'Zzzz',
        );

        self::assertNull($result['values']['champion']);
        self::assertSame('3006', $result['values']['item']);
        self::assertSame([FavoriteSlot::Champion], $result['invalid']);
    }

    public function testOversizedIdIsDroppedWithoutProbingExistence(): void
    {
        $existenceProbed = false;
        $result = $this->sanitizer->sanitize(
            ['item' => str_repeat('9', 17)],
            [],
            static function () use (&$existenceProbed): bool {
                $existenceProbed = true;

                return true;
            },
        );

        self::assertNull($result['values']['item']);
        self::assertSame([FavoriteSlot::Item], $result['invalid']);
        self::assertFalse($existenceProbed, 'length guard runs before the (potentially costly) existence check');
    }

    public function testUnchangedFavoriteAbsentFromPatchIsPreservedNotWiped(): void
    {
        // Smolder exists on latest but not on the viewed old patch; the island
        // round-trips the stored id, so a mere visibility toggle must not wipe it.
        $result = $this->sanitizer->sanitize(
            ['champion' => 'Smolder'],
            ['champion' => 'Smolder'],
            static fn (): bool => false,
        );

        self::assertSame('Smolder', $result['values']['champion']);
        self::assertSame([], $result['invalid'], 'an unchanged off-patch favorite is preserved silently');
    }

    public function testAbsentIdThatDiffersFromStoredIsStillDropped(): void
    {
        // A newly submitted id that does not exist and is not the stored one is a
        // genuine bad pick — dropped with a warning even when a favorite is stored.
        $result = $this->sanitizer->sanitize(
            ['champion' => 'Ghostwalker'],
            ['champion' => 'Aatrox'],
            static fn (): bool => false,
        );

        self::assertNull($result['values']['champion']);
        self::assertSame([FavoriteSlot::Champion], $result['invalid']);
    }
}
