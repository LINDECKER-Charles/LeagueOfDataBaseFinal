<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Profile;

use App\Entity\User;
use App\Service\Picker\PickerCatalog;
use App\Service\Profile\FavoriteSlot;
use App\Service\Profile\FavoriteSlots;
use PHPUnit\Framework\TestCase;

/**
 * Completeness guard on the slot <-> User column bridge. storedIds() and apply()
 * spell the four columns out by hand (explicit, typed accessors — a deliberate
 * trade-off against dynamic ones): nothing but this test breaks when a fifth
 * FavoriteSlot case is added and one of the two lists is forgotten.
 *
 * The catalog collaborator is never reached by these two methods, so a bare
 * instance stands in for it.
 */
final class FavoriteSlotsTest extends TestCase
{
    private FavoriteSlots $slots;

    protected function setUp(): void
    {
        $catalog = (new \ReflectionClass(PickerCatalog::class))->newInstanceWithoutConstructor();
        $this->slots = new FavoriteSlots($catalog);
    }

    public function testEverySlotIsReadableAndWritableOnTheUser(): void
    {
        $values = [];
        foreach (FavoriteSlot::cases() as $slot) {
            $values[$slot->value] = 'id-'.$slot->value;
        }

        $user = new User();
        $this->slots->apply($user, $values);

        self::assertSame(
            $values,
            $this->slots->storedIds($user),
            'each FavoriteSlot case must round-trip through apply()/storedIds()',
        );
    }

    public function testMissingValuesClearTheirColumn(): void
    {
        $user = new User();
        $user->setFavoriteChampionId('Aatrox');
        $user->setFavoriteItemId('3006');

        $this->slots->apply($user, [FavoriteSlot::Champion->value => 'Ahri']);

        self::assertSame(
            [
                FavoriteSlot::Champion->value => 'Ahri',
                FavoriteSlot::Item->value => null,
                FavoriteSlot::Rune->value => null,
                FavoriteSlot::Summoner->value => null,
            ],
            $this->slots->storedIds($user),
        );
    }
}
