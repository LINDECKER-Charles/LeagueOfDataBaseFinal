<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API;

use App\Service\API\ChampionManager;
use App\Service\API\ItemManager;
use App\Service\API\ResourceNotFoundException;
use App\Service\API\RuneManager;
use App\Service\API\SummonerManager;

/**
 * getByName()/searchByName() now live once in {@see \App\Service\API\Concern\ResolvesEntries};
 * these lock the per-resource behaviour the four managers used to hand-copy:
 * where an entry is looked up from, what a query matches, and what a hit looks
 * like (notably the item id, which comes from the map key and stays an int).
 */
final class ManagerEntryLookupTest extends SeededManagerTestCase
{
    public function testChampionIsLookedUpByItsMapKey(): void
    {
        $entry = $this->manager(ChampionManager::class)
            ->getByName('Karma', self::VERSION, self::LANG);

        self::assertSame('Karma', $entry['name']);
    }

    public function testSummonerIsLookedUpByItsSpellId(): void
    {
        $entry = $this->manager(SummonerManager::class)
            ->getByName('SummonerFlash', self::VERSION, self::LANG);

        self::assertSame('Flash', $entry['name']);
    }

    /** Runes are a top-level list: the route id is the tree key, not a map key. */
    public function testRuneTreeIsLookedUpByItsKey(): void
    {
        $entry = $this->manager(RuneManager::class)
            ->getByName('Precision', self::VERSION, self::LANG);

        self::assertSame(8000, $entry['id']);
    }

    /**
     * @return iterable<string, array{0: class-string, 1: string}>
     */
    public static function unknownEntries(): iterable
    {
        yield 'champion' => [ChampionManager::class, 'Nope'];
        yield 'item'     => [ItemManager::class, '999999'];
        yield 'summoner' => [SummonerManager::class, 'SummonerNope'];
        yield 'rune'     => [RuneManager::class, 'Nope'];
    }

    /**
     * @param class-string $manager
     */
    #[\PHPUnit\Framework\Attributes\DataProvider('unknownEntries')]
    public function testUnknownEntryIsADefinitiveAbsence(string $manager, string $name): void
    {
        $this->expectException(ResourceNotFoundException::class);

        $this->manager($manager)->getByName($name, self::VERSION, self::LANG);
    }

    public function testChampionSearchMatchesTheIdAsWellAsTheDisplayName(): void
    {
        $hits = $this->manager(ChampionManager::class)
            ->searchByName('monkey', $this->dataset());

        self::assertSame(['Wukong'], array_column($hits, 'name'));
    }

    public function testChampionSearchStopsAtTheRequestedMaximum(): void
    {
        $manager = $this->manager(ChampionManager::class);

        self::assertCount(2, $manager->searchByName('ka', $this->dataset()));
        self::assertCount(1, $manager->searchByName('ka', $this->dataset(), 1));
    }

    /** Items carry no id field: matching one would surface unrelated rows. */
    public function testItemSearchMatchesTheNameOnly(): void
    {
        $manager = $this->manager(ItemManager::class);

        self::assertSame(
            ['Long Sword'],
            array_column($manager->searchByName('sword', $this->dataset()), 'name'),
        );
        self::assertSame([], $manager->searchByName('1036', $this->dataset()));
    }

    /**
     * The item id lives in the map key, and PHP recasts numeric keys to int —
     * the JSON search endpoint has always exposed it as a number.
     */
    public function testItemSearchInjectsTheMapKeyAsTheEntryId(): void
    {
        $hits = $this->manager(ItemManager::class)
            ->searchByName('sword', $this->dataset());

        self::assertSame(1036, $hits[0]['id']);
    }

    public function testSummonerSearchMatchesTheIdAsWellAsTheDisplayName(): void
    {
        $manager = $this->manager(SummonerManager::class);
        $byId    = $manager->searchByName('summonerflash', $this->dataset());
        $byName  = $manager->searchByName('heal', $this->dataset());

        self::assertSame(['Flash'], array_column($byId, 'name'));
        self::assertSame(['Heal'], array_column($byName, 'name'));
    }

    public function testTooShortQueryIsRejectedBeforeAnyDataAccess(): void
    {
        $this->expectException(\InvalidArgumentException::class);

        $this->manager(ChampionManager::class)->searchByName('k', $this->dataset());
    }
}
