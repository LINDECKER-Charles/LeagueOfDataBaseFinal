<?php
declare(strict_types=1);

namespace App\Tests\Unit\Repository;

use App\Entity\Build;
use App\Entity\User;
use App\Repository\BuildRepository;
use App\Tests\Unit\Support\InMemoryOrm;
use Doctrine\ORM\EntityManager;
use Doctrine\Persistence\ManagerRegistry;
use PHPUnit\Framework\TestCase;

/**
 * Moderation search against real SQL: name/champion needle, visibility filter,
 * pagination and the global counters.
 */
final class BuildRepositoryModerationTest extends TestCase
{
    private EntityManager $entityManager;
    private BuildRepository $builds;

    protected function setUp(): void
    {
        $this->entityManager = InMemoryOrm::entityManager([User::class, Build::class]);
        $registry = $this->createStub(ManagerRegistry::class);
        $registry->method('getManagerForClass')->willReturn($this->entityManager);
        $this->builds = new BuildRepository($registry);
    }

    public function testSearchMatchesNameOrChampionCaseInsensitively(): void
    {
        $this->givenBuild('Lethality one-shot', 'Zed', isPublic: true);
        $this->givenBuild('Tank build', 'Ornn', isPublic: true);

        self::assertSame(1, $this->builds->searchPaginated('LETHALITY', null, 1, 25)['total']);
        self::assertSame(1, $this->builds->searchPaginated('ornn', null, 1, 25)['total']);
        self::assertSame(0, $this->builds->searchPaginated('nothing', null, 1, 25)['total']);
    }

    public function testVisibilityFilterAndCounters(): void
    {
        $this->givenBuild('Public A', 'Ahri', isPublic: true);
        $this->givenBuild('Public B', 'Ashe', isPublic: true);
        $this->givenBuild('Draft', 'Ahri', isPublic: false);

        self::assertSame(2, $this->builds->searchPaginated('', true, 1, 25)['total']);
        self::assertSame(1, $this->builds->searchPaginated('', false, 1, 25)['total']);
        self::assertSame(3, $this->builds->countAll());
        self::assertSame(2, $this->builds->countPublic());
    }

    public function testPaginationIsStable(): void
    {
        foreach (range(1, 3) as $i) {
            $this->givenBuild('Build ' . $i, 'Ahri', isPublic: true);
        }

        $pageTwo = $this->builds->searchPaginated('', null, 2, 2);

        self::assertSame(3, $pageTwo['total']);
        self::assertCount(1, $pageTwo['builds']);
    }

    private function givenBuild(string $name, string $championId, bool $isPublic): Build
    {
        $owner = new User()
            ->setEmail('owner-' . bin2hex(random_bytes(4)) . '@example.test')
            ->setUsername('owner' . bin2hex(random_bytes(4)));
        $this->entityManager->persist($owner);

        $build = new Build()
            ->setOwner($owner)
            ->setName($name)
            ->setChampionId($championId)
            ->setGameVersion('16.14.1')
            ->setRunes([])
            ->setSteps([])
            ->setIsPublic($isPublic);
        $this->entityManager->persist($build);
        $this->entityManager->flush();

        return $build;
    }
}
