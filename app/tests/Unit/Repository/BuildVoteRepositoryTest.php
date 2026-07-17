<?php
declare(strict_types=1);

namespace App\Tests\Unit\Repository;

use App\Entity\Build;
use App\Entity\BuildVote;
use App\Entity\User;
use App\Repository\BuildVoteRepository;
use App\Service\Picker\GameMode;
use App\Tests\Unit\Support\InMemoryOrm;
use Doctrine\ORM\EntityManager;
use Doctrine\Persistence\ManagerRegistry;
use PHPUnit\Framework\TestCase;

/**
 * Vote semantics against real SQL (in-memory SQLite): toggle/switch upsert,
 * aggregated net scores, and the trends ranking (public only, LEFT JOIN so
 * zero-vote builds appear, score DESC then newest first).
 */
final class BuildVoteRepositoryTest extends TestCase
{
    private EntityManager $entityManager;
    private BuildVoteRepository $votes;

    protected function setUp(): void
    {
        $this->entityManager = InMemoryOrm::entityManager([User::class, Build::class, BuildVote::class]);
        $registry = $this->createStub(ManagerRegistry::class);
        $registry->method('getManagerForClass')->willReturn($this->entityManager);
        $this->votes = new BuildVoteRepository($registry);
    }

    public function testFirstVoteInsertsAndScores(): void
    {
        [$build, $voter] = [$this->givenPublicBuild('Aatrox'), $this->givenUser('voter-a')];

        $this->votes->applyVote($build, $voter, BuildVote::UP);

        self::assertSame([$build->getId() => 1], $this->votes->scoreFor([$build->getId()]));
        self::assertSame(BuildVote::UP, $this->votes->findOneByBuildAndVoter($build, $voter)?->getValue());
    }

    public function testRevotingTheSameValueWithdrawsTheVote(): void
    {
        [$build, $voter] = [$this->givenPublicBuild('Aatrox'), $this->givenUser('voter-a')];

        $this->votes->applyVote($build, $voter, BuildVote::UP);
        $this->votes->applyVote($build, $voter, BuildVote::UP);

        self::assertSame([], $this->votes->scoreFor([$build->getId()]));
        self::assertNull($this->votes->findOneByBuildAndVoter($build, $voter));
    }

    public function testChangingOnesMindReplacesTheVoteInPlace(): void
    {
        [$build, $voter] = [$this->givenPublicBuild('Aatrox'), $this->givenUser('voter-a')];

        $this->votes->applyVote($build, $voter, BuildVote::UP);
        $this->votes->applyVote($build, $voter, BuildVote::DOWN);

        self::assertSame([$build->getId() => -1], $this->votes->scoreFor([$build->getId()]));
        self::assertSame(1, $this->votes->count([])); // replaced, not duplicated
    }

    public function testInvalidVoteValueIsRejected(): void
    {
        $this->expectException(\InvalidArgumentException::class);

        $this->votes->applyVote($this->givenPublicBuild('Aatrox'), $this->givenUser('voter-a'), 2);
    }

    public function testScoreForAggregatesPerBuildInOneQuery(): void
    {
        $liked = $this->givenPublicBuild('Ahri');
        $contested = $this->givenPublicBuild('Ashe');
        $unvoted = $this->givenPublicBuild('Amumu');
        [$a, $b, $c] = [$this->givenUser('va'), $this->givenUser('vb'), $this->givenUser('vc')];

        $this->votes->applyVote($liked, $a, BuildVote::UP);
        $this->votes->applyVote($liked, $b, BuildVote::UP);
        $this->votes->applyVote($liked, $c, BuildVote::UP);
        $this->votes->applyVote($contested, $a, BuildVote::UP);
        $this->votes->applyVote($contested, $b, BuildVote::DOWN);
        $this->votes->applyVote($contested, $c, BuildVote::DOWN);

        $scores = $this->votes->scoreFor([$liked->getId(), $contested->getId(), $unvoted->getId()]);

        self::assertSame(3, $scores[$liked->getId()]);
        self::assertSame(-1, $scores[$contested->getId()]);
        self::assertArrayNotHasKey($unvoted->getId(), $scores);
    }

    public function testValuesForReturnsOnlyTheVotersOwnVerdicts(): void
    {
        $build = $this->givenPublicBuild('Ahri');
        $other = $this->givenPublicBuild('Ashe');
        [$me, $someone] = [$this->givenUser('me'), $this->givenUser('someone')];

        $this->votes->applyVote($build, $me, BuildVote::DOWN);
        $this->votes->applyVote($other, $someone, BuildVote::UP);

        self::assertSame([$build->getId() => -1], $this->votes->valuesFor($me, [$build->getId(), $other->getId()]));
    }

    public function testTopPublicBuildsRanksByScoreThenNewestAndKeepsZeroVoteBuilds(): void
    {
        $top = $this->givenPublicBuild('Ahri', createdDaysAgo: 3);
        $oldSilent = $this->givenPublicBuild('Amumu', createdDaysAgo: 2);
        $newSilent = $this->givenPublicBuild('Anivia', createdDaysAgo: 1);
        $downvoted = $this->givenPublicBuild('Ashe', createdDaysAgo: 4);
        $this->givenBuild('Aatrox', isPublic: false); // must never surface

        $voter = $this->givenUser('ranker');
        $this->votes->applyVote($top, $voter, BuildVote::UP);
        $this->votes->applyVote($downvoted, $voter, BuildVote::DOWN);

        $result = $this->votes->topPublicBuilds(null, null, page: 1, perPage: 24);

        self::assertSame(4, $result['total']);
        self::assertSame(
            // +1 first, then the two 0-vote builds newest-first, then the -1.
            [$top->getId(), $newSilent->getId(), $oldSilent->getId(), $downvoted->getId()],
            array_map(static fn (Build $b): ?int => $b->getId(), $result['builds']),
        );
    }

    public function testTopPublicBuildsExcludesBannedOwners(): void
    {
        $bannedOwner = $this->givenUser('banned-owner');
        $this->givenPublicBuild('Ahri', owner: $bannedOwner);
        $visible = $this->givenPublicBuild('Ashe');
        $bannedOwner->ban('spam');
        $this->entityManager->flush();

        $result = $this->votes->topPublicBuilds(null, null, page: 1, perPage: 24);

        self::assertSame(1, $result['total']);
        self::assertSame([$visible->getId()], array_map(static fn (Build $b): ?int => $b->getId(), $result['builds']));

        $bannedOwner->unban();
        $this->entityManager->flush();
        self::assertSame(2, $this->votes->topPublicBuilds(null, null, 1, 24)['total']);
    }

    public function testTopPublicBuildsFiltersByChampionAndModeAndPaginates(): void
    {
        $aram = $this->givenPublicBuild('Ahri', GameMode::Aram);
        $this->givenPublicBuild('Ahri');
        $this->givenPublicBuild('Ashe');

        $byChampion = $this->votes->topPublicBuilds('Ahri', null, 1, 24);
        self::assertSame(2, $byChampion['total']);

        $byMode = $this->votes->topPublicBuilds('Ahri', GameMode::Aram, 1, 24);
        self::assertSame(1, $byMode['total']);
        self::assertSame($aram->getId(), $byMode['builds'][0]->getId());

        $pageTwo = $this->votes->topPublicBuilds(null, null, page: 2, perPage: 2);
        self::assertSame(3, $pageTwo['total']);
        self::assertCount(1, $pageTwo['builds']);
    }

    private function givenUser(string $username): User
    {
        $user = new User()->setEmail($username . '@example.test')->setUsername($username);
        $this->entityManager->persist($user);
        $this->entityManager->flush();

        return $user;
    }

    private function givenPublicBuild(
        string $championId,
        GameMode $mode = GameMode::SummonersRift,
        int $createdDaysAgo = 0,
        ?User $owner = null,
    ): Build {
        return $this->givenBuild($championId, true, $mode, $createdDaysAgo, $owner);
    }

    private function givenBuild(
        string $championId,
        bool $isPublic,
        GameMode $mode = GameMode::SummonersRift,
        int $createdDaysAgo = 0,
        ?User $owner = null,
    ): Build {
        $build = new Build()
            ->setOwner($owner ?? $this->givenUser('owner-' . bin2hex(random_bytes(4))))
            ->setName($championId . ' build')
            ->setChampionId($championId)
            ->setGameVersion('16.14.1')
            ->setGameMode($mode)
            ->setRunes([])
            ->setSteps([])
            ->setIsPublic($isPublic);

        if ($createdDaysAgo > 0) {
            // No public mutator on purpose (creation date is immutable in prod);
            // the ranking tests need distinct timestamps, hence reflection.
            new \ReflectionProperty(Build::class, 'createdAt')
                ->setValue($build, new \DateTimeImmutable("-{$createdDaysAgo} days"));
        }

        $this->entityManager->persist($build);
        $this->entityManager->flush();

        return $build;
    }
}
