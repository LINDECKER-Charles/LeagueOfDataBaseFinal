<?php
declare(strict_types=1);

namespace App\Controller\Build;

use App\Controller\AbstractPageController;
use App\Controller\Concern\OwnsBuilds;
use App\Controller\Concern\RendersBuildEditor;
use App\Controller\Concern\ResolvesCurrentUser;
use App\Entity\Build;
use App\Repository\BuildRepository;
use App\Service\Build\BuildCatalogGate;
use App\Service\Build\BuildStructureProjector;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Cross-version import: forward-/back-ports one of the owner's builds onto a
 * target patch, keeping only the components that exist there and opening a fresh
 * (create-mode) editor with the ported draft — the source is untouched, and
 * nothing is written until the owner saves.
 *
 * Split from {@see BuildController}: projecting a structure onto another patch's
 * catalogs is a different story from authoring one, with its own service
 * ({@see BuildStructureProjector}) and its own player-facing report.
 */
final class BuildImportController extends AbstractPageController
{
    use OwnsBuilds;
    use RendersBuildEditor;
    use ResolvesCurrentUser;

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly BuildRepository $builds,
        private readonly BuildCatalogGate $catalogGate,
        private readonly BuildStructureProjector $projector,
        private readonly TranslatorInterface $translator,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route(
        '/builds/{id}/import',
        name: 'app_build_import',
        requirements: ['id' => '\d+'],
        methods: ['GET'],
    )]
    public function import(Request $request, int $id): Response
    {
        if ($guard = $this->requireVerifiedEmail()) {
            return $guard;
        }

        $source = $this->ownedOr404($id);
        $target = $this->targetVersion($request);
        $lang = $this->pageContext->selection()['lang'];

        try {
            $catalogs = $this->catalogGate->catalogs($target, $lang);
        } catch (\Throwable) {
            return $this->backToSourceEditor($id);
        }

        $result = $this->projector->project(
            self::structureOf($source),
            $source->getGameMode(),
            $catalogs,
        );
        $this->flashImportReport($result['report'], $target);

        return $this->editorResponse(null, $this->portedDraft($source, $target, $result));
    }

    /**
     * Import target: the explicit ?to= if a known patch, else the current
     * browsing patch (latest by default).
     */
    private function targetVersion(Request $request): string
    {
        $to = trim((string) $request->query->get('to', ''));
        if ($to !== '' && $this->versionManager->versionExists($to)) {
            return $to;
        }

        return $this->pageContext->selection()['version'];
    }

    /** @return array{championId: ?string, runes: array<mixed>, steps: array<mixed>} */
    private static function structureOf(Build $build): array
    {
        return [
            'championId' => $build->getChampionId(),
            'runes'      => $build->getRunes(),
            'steps'      => $build->getSteps(),
        ];
    }

    /**
     * The ported build as a fresh create-mode draft: private until the owner
     * publishes it, pinned on the target patch, keeping the source's identity.
     *
     * @param array{structure: array<mixed>, report: array<mixed>} $result
     * @return array<string, mixed>
     */
    private function portedDraft(Build $source, string $target, array $result): array
    {
        return [
            'name'        => $source->getName(),
            'description' => $source->getDescription(),
            'isPublic'    => false,
            'structure'   => $result['structure'],
            'gameVersion' => $target,
            'gameMode'    => $source->getGameMode()->value,
            'language'    => $source->getLanguage(),
        ];
    }

    /** Catalogs unreachable: the owner lands back on the source build's editor. */
    private function backToSourceEditor(int $id): Response
    {
        $this->addFlash('error', $this->translator->trans('build.error.catalog_unavailable'));

        return $this->redirectToRoute('app_build_edit', ['id' => $id], Response::HTTP_SEE_OTHER);
    }

    /**
     * What the port had to give up, told to the player: the champion missing from
     * the target patch, runes reset, items dropped.
     *
     * @param array{
     *     championMissing: bool,
     *     runesReset: bool,
     *     droppedItems: list<array{step: int, id: string, name: string}>
     * } $report
     */
    private function flashImportReport(array $report, string $target): void
    {
        $this->addFlash(
            'success',
            $this->translator->trans('build.import.done', ['%version%' => $target]),
        );

        if ($report['championMissing']) {
            $this->addFlash('warning', $this->translator->trans('build.import.champion_missing'));
        }
        if ($report['runesReset']) {
            $this->addFlash('warning', $this->translator->trans('build.import.runes_reset'));
        }
        if ($report['droppedItems'] !== []) {
            // One mention per item id, not per name: an item dropped from several
            // steps reads once, a classic twin is not folded into its namesake.
            $byId  = array_column($report['droppedItems'], 'name', 'id');
            $names = implode(', ', $byId);
            $this->addFlash(
                'warning',
                $this->translator->trans('build.import.items_dropped', ['%items%' => $names]),
            );
        }
    }
}
