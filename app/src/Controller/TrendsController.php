<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\Build;
use App\Entity\User;
use App\Repository\BuildRepository;
use App\Repository\BuildVoteRepository;
use App\Service\API\ChampionManager;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Community\TrendsFilter;
use App\Service\Community\TrendsViewAssembler;
use App\Service\Picker\GameMode;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Public, indexable trends page: every public build ranked by net vote score.
 * Filters (champion, mode, authoring language) and the page live in the query
 * string — server rendered so the ranking is crawlable; only the vote controls
 * hydrate as a Vue island. The champion and language filters offer only values
 * that actually have a public build (derived from the repository, not the full
 * catalog).
 */
final class TrendsController extends AbstractResourceController
{
    public const PER_PAGE = 24;

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly BuildRepository $builds,
        private readonly BuildVoteRepository $votes,
        private readonly TrendsViewAssembler $assembler,
        private readonly ChampionManager $champions,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/trends', name: 'app_trends', methods: ['GET'])]
    public function index(Request $request): Response
    {
        ['version' => $version, 'lang' => $lang] = $this->pageContext->selection();
        $championId = trim((string) $request->query->get('champion', '')) ?: null;
        $mode = GameMode::tryFrom((string) $request->query->get('mode', ''));
        $language = $this->requestedLanguage($request);
        $page = max(1, $request->query->getInt('page', 1));

        $filter = new TrendsFilter($championId, $mode, $language);
        ['builds' => $pageBuilds, 'total' => $total] = $this->votes
            ->topPublicBuilds($filter, $page, self::PER_PAGE);

        return $this->render('trends/index.html.twig', [
            'client' => $this->clientData(),
            'rows' => $this->rows($pageBuilds, $version, $lang),
            'total' => $total,
            'page' => $page,
            'pages' => max(1, (int) ceil($total / self::PER_PAGE)),
            'filters' => ['champion' => $championId, 'mode' => $mode?->value, 'language' => $language],
            'championOptions' => $this->championOptions($version, $lang),
            'languageOptions' => $this->languageOptions(),
            'gameModes' => GameMode::cases(),
        ]);
    }

    /** The ?language= facet, kept only when it is a known Data Dragon locale (else: no restriction). */
    private function requestedLanguage(Request $request): ?string
    {
        $language = trim((string) $request->query->get('language', '')) ?: null;

        return $language !== null && $this->versionManager->languageExists($language) ? $language : null;
    }

    /**
     * Authoring locales present among public builds, labeled from the Data Dragon
     * label map (raw code as fallback), sorted by label.
     *
     * @return list<array{value: string, label: string}>
     */
    private function languageOptions(): array
    {
        $labels = $this->versionManager->getLanguageLabels();
        $options = array_map(
            static fn (string $code): array => ['value' => $code, 'label' => $labels[$code] ?? $code],
            $this->builds->distinctPublicLanguages(),
        );
        usort($options, static fn (array $a, array $b): int => strcasecmp($a['label'], $b['label']));

        return $options;
    }

    /**
     * Row view-models with their net score and the visitor's own vote zipped in.
     *
     * @param list<Build> $pageBuilds
     * @return list<array<string, mixed>>
     */
    private function rows(array $pageBuilds, string $version, string $lang): array
    {
        $ids = array_map(static fn (Build $build): int => (int) $build->getId(), $pageBuilds);
        $scores = $this->votes->scoreFor($ids);
        $user = $this->getUser();
        $myVotes = $user instanceof User ? $this->votes->valuesFor($user, $ids) : [];

        return array_map(
            static function (array $row) use ($scores, $myVotes): array {
                $id = (int) $row['build']->getId();

                return $row + ['score' => $scores[$id] ?? 0, 'myVote' => $myVotes[$id] ?? 0];
            },
            $this->assembler->assemble($pageBuilds, $version, $lang),
        );
    }

    /**
     * Champions with at least one public build, labeled from the current
     * catalog — best effort: on catalog failure the raw ids still filter fine.
     *
     * @return list<array{id: string, name: string}>
     */
    private function championOptions(string $version, string $lang): array
    {
        $ids = $this->builds->distinctPublicChampionIds();
        try {
            $names = $this->champions->listIndex($version, $lang);
        } catch (\Throwable) {
            $names = [];
        }

        $options = array_map(
            static fn (string $id): array => ['id' => $id, 'name' => (string) ($names[$id] ?? $id)],
            $ids,
        );
        usort($options, static fn (array $a, array $b): int => strcasecmp($a['name'], $b['name']));

        return $options;
    }
}
