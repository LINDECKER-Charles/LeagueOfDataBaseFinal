<?php
declare(strict_types=1);

namespace App\Controller;

use App\Controller\Concern\ResolvesCurrentUser;
use App\Entity\Build;
use App\Repository\BuildRepository;
use App\Service\Build\BuildCatalogGate;
use App\Service\Build\BuildStructureNormalizer;
use App\Service\Build\BuildStructureProjector;
use App\Service\Build\BuildSubmission;
use App\Service\Build\BuildViewAssembler;
use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\AuditTarget;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Picker\GameMode;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Authenticated build CRUD (everything under ^/builds is ROLE_USER by
 * access_control). Ownership failures are 404 — not 403 — so build ids leak no
 * existence oracle. Validation errors re-render the editor with the submitted
 * structure re-proposed (no input loss) and a 422 so Turbo swaps the page.
 */
final class BuildController extends AbstractResourceController
{
    use ResolvesCurrentUser;

    private const CSRF_SUBMIT = 'submit';
    private const CSRF_DELETE_PREFIX = 'delete-build-';
    private const ERROR_VERSION_UNKNOWN = 'build.error.version.unknown';
    /** Patches offered by the editor's version select (latest first); the build's own patch is always kept. */
    private const VERSION_CHOICES_MAX = 30;

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly BuildRepository $builds,
        private readonly BuildCatalogGate $catalogGate,
        private readonly BuildStructureNormalizer $normalizer,
        private readonly BuildStructureProjector $projector,
        private readonly BuildViewAssembler $assembler,
        private readonly EntityManagerInterface $entityManager,
        private readonly TranslatorInterface $translator,
        private readonly AuditLogger $audit,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/builds', name: 'app_builds', methods: ['GET'])]
    public function index(): Response
    {
        $sel = $this->pageContext->selection();
        $rows = array_map(
            fn (Build $build): array => ['build' => $build]
                + $this->assembler->listRow($build, $sel['version'], $sel['lang']),
            $this->builds->findOwnedBy($this->currentUser()),
        );

        return $this->render('build/index.html.twig', [
            'client' => $this->clientData(),
            'rows' => $rows,
        ]);
    }

    #[Route('/builds/new', name: 'app_build_new', methods: ['GET'])]
    public function new(): Response
    {
        return $this->requireVerifiedEmail() ?? $this->editorResponse(null, self::emptyValues());
    }

    #[Route('/builds', name: 'app_build_create', methods: ['POST'])]
    public function create(Request $request): Response
    {
        return $this->handleSubmit($request, null);
    }

    #[Route('/builds/{id}/edit', name: 'app_build_edit', requirements: ['id' => '\d+'], methods: ['GET'])]
    public function edit(int $id): Response
    {
        $build = $this->ownedOr404($id);

        return $this->editorResponse($build, [
            'name' => $build->getName(),
            'description' => $build->getDescription(),
            'isPublic' => $build->isPublic(),
            'structure' => [
                'championId' => $build->getChampionId(),
                'runes' => $build->getRunes(),
                'steps' => $build->getSteps(),
            ],
            // Editing is PINNED on the build's own patch and mode; the version
            // select still allows moving the build to another patch explicitly.
            'gameVersion' => $build->getGameVersion(),
            'gameMode' => $build->getGameMode()->value,
        ]);
    }

    #[Route('/builds/{id}', name: 'app_build_update', requirements: ['id' => '\d+'], methods: ['POST'])]
    public function update(Request $request, int $id): Response
    {
        return $this->handleSubmit($request, $this->ownedOr404($id));
    }

    /**
     * Cross-version import: forward-/back-ports one of the owner's builds onto a
     * target patch, keeping only the components that exist there and opening a
     * fresh (create-mode) editor with the ported draft — the source is untouched.
     */
    #[Route('/builds/{id}/import', name: 'app_build_import', requirements: ['id' => '\d+'], methods: ['GET'])]
    public function import(Request $request, int $id): Response
    {
        if ($guard = $this->requireVerifiedEmail()) {
            return $guard;
        }

        $source = $this->ownedOr404($id);
        $target = $this->importTargetVersion($request);
        $mode   = $source->getGameMode();

        try {
            $catalogs = $this->catalogGate->catalogs($target, $this->pageContext->selection()['lang']);
        } catch (\Throwable) {
            $this->addFlash('error', $this->translator->trans('build.error.catalog_unavailable'));

            return $this->redirectToRoute('app_build_edit', ['id' => $id], Response::HTTP_SEE_OTHER);
        }

        $result = $this->projector->project([
            'championId' => $source->getChampionId(),
            'runes'      => $source->getRunes(),
            'steps'      => $source->getSteps(),
        ], $mode, $catalogs);
        $this->flashImportReport($result['report'], $target);

        return $this->editorResponse(null, [
            'name'        => $source->getName(),
            'description' => $source->getDescription(),
            'isPublic'    => false,
            'structure'   => $result['structure'],
            'gameVersion' => $target,
            'gameMode'    => $mode->value,
        ]);
    }

    #[Route('/builds/{id}/delete', name: 'app_build_delete', requirements: ['id' => '\d+'], methods: ['POST'])]
    public function delete(Request $request, int $id): Response
    {
        $build = $this->ownedOr404($id);

        if (!$this->isCsrfTokenValid(self::CSRF_DELETE_PREFIX.$id, (string) $request->request->get('_token'))) {
            $this->addFlash('error', $this->translator->trans('build.error.csrf'));

            return $this->redirectToRoute('app_builds', status: Response::HTTP_SEE_OTHER);
        }

        $target = AuditTarget::of(AuditTarget::TYPE_BUILD, $build->getId(), $build->getName());
        $this->entityManager->remove($build);
        $this->entityManager->flush();
        $this->audit->log(AuditAction::BuildDelete, target: $target);
        $this->addFlash('success', $this->translator->trans('build.flash.deleted'));

        return $this->redirectToRoute('app_builds', status: Response::HTTP_SEE_OTHER);
    }

    /** Shared create/update pipeline: verified email → CSRF → field errors → catalog validation → persist. */
    private function handleSubmit(Request $request, ?Build $build): Response
    {
        if ($guard = $this->requireVerifiedEmail()) {
            return $guard;
        }

        $submission = BuildSubmission::fromRequest($request, $this->pageContext->selection()['version']);
        $values = [
            'name' => $submission->name,
            'description' => $submission->description,
            'isPublic' => $submission->isPublic,
            'structure' => $submission->structure,
            'gameVersion' => $submission->gameVersion,
            'gameMode' => ($submission->gameMode ?? GameMode::DEFAULT)->value,
        ];

        if (!$this->isCsrfTokenValid(self::CSRF_SUBMIT, (string) $request->request->get('_token'))) {
            return $this->editorErrorResponse([['build.error.csrf', []]], $build, $values);
        }

        $errors = $this->collectErrors($submission);
        if ($errors !== []) {
            return $this->editorErrorResponse($errors, $build, $values);
        }

        $isNew = $build === null;
        $build = $this->persistSubmission($build, $submission);
        $this->audit->log(
            $isNew ? AuditAction::BuildCreate : AuditAction::BuildUpdate,
            target: AuditTarget::of(AuditTarget::TYPE_BUILD, $build->getId(), $build->getName()),
        );
        $this->addFlash('success', $this->translator->trans($isNew ? 'build.flash.created' : 'build.flash.updated'));

        return $this->redirectToRoute(
            'app_build_show',
            ['token' => $build->getShareToken()],
            Response::HTTP_SEE_OTHER,
        );
    }

    /** @return list<array{0: string, 1: array<string, string>}> translator-ready (code, params) tuples */
    private function collectErrors(BuildSubmission $submission): array
    {
        $errors = array_map(static fn (string $code): array => [$code, []], $submission->formErrors());
        $isVersionKnown = $this->versionManager->versionExists($submission->gameVersion);
        if (!$isVersionKnown) {
            $errors[] = [self::ERROR_VERSION_UNKNOWN, []];
        }
        // No structure / unknown mode / unknown version: the catalogs to check
        // against are undefined — report what we already know.
        if ($submission->structure === null || $submission->gameMode === null || !$isVersionKnown) {
            return $errors;
        }

        try {
            return [...$errors, ...$this->catalogGate->validate(
                $submission->structure,
                $submission->gameVersion,
                $this->pageContext->selection()['lang'],
                $submission->gameMode,
            )];
        } catch (\Throwable) {
            // Transient catalog outage: refuse the write honestly rather than
            // accepting an unverified structure or 500ing away the user's input.
            return [...$errors, ['build.error.catalog_unavailable', []]];
        }
    }

    private function persistSubmission(?Build $build, BuildSubmission $submission): Build
    {
        if ($build === null) {
            $build = (new Build())->setOwner($this->currentUser());
            $this->entityManager->persist($build);
        }

        $structure = $this->normalizer->normalize((array) $submission->structure);
        $build->setName($submission->name)
            ->setDescription($submission->description)
            ->setIsPublic($submission->isPublic)
            ->setChampionId($structure['championId'])
            ->setRunes($structure['runes'])
            ->setSteps($structure['steps'])
            // The structure was validated against the SUBMITTED (version, mode):
            // persist exactly that pair — the build stays pinned to its patch.
            ->setGameVersion($submission->gameVersion)
            ->setGameMode($submission->gameMode ?? GameMode::DEFAULT);
        $this->entityManager->flush();

        return $build;
    }

    /** @param list<array{0: string, 1: array<string, string>}> $errors */
    private function editorErrorResponse(array $errors, ?Build $build, array $values): Response
    {
        foreach ($errors as [$code, $params]) {
            $this->addFlash('error', $this->translator->trans($code, $params));
        }

        return $this->editorResponse($build, $values, Response::HTTP_UNPROCESSABLE_ENTITY);
    }

    /**
     * @param array{name: string, description: ?string, isPublic: bool, structure: ?array<mixed>,
     *               gameVersion: ?string, gameMode: string} $values
     */
    private function editorResponse(?Build $build, array $values, int $status = Response::HTTP_OK): Response
    {
        $sel = $this->pageContext->selection();
        $selectedVersion = ($values['gameVersion'] ?? '') !== '' ? (string) $values['gameVersion'] : $sel['version'];

        return $this->render('build/editor.html.twig', [
            'client' => $this->clientData(),
            'mode' => $build === null ? 'create' : 'edit',
            'build' => $build,
            'values' => $values,
            'version' => $selectedVersion,
            'gameMode' => $values['gameMode'],
            'gameModes' => GameMode::cases(),
            'versionChoices' => $this->versionChoices($selectedVersion),
            'lang' => $sel['lang'],
        ], new Response(status: $status));
    }

    /**
     * Latest patches for the version select, capped to keep the list usable —
     * plus the currently selected one, so an old build stays editable pinned.
     *
     * @return list<string>
     */
    private function versionChoices(string $selected): array
    {
        $choices = array_slice($this->versionManager->getVersions(), 0, self::VERSION_CHOICES_MAX);
        if ($selected !== '' && !in_array($selected, $choices, true)) {
            $choices[] = $selected;
        }

        return $choices;
    }

    /** Import target: the explicit ?to= if a known patch, else the current browsing patch (latest by default). */
    private function importTargetVersion(Request $request): string
    {
        $to = trim((string) $request->query->get('to', ''));
        if ($to !== '' && $this->versionManager->versionExists($to)) {
            return $to;
        }

        return $this->pageContext->selection()['version'];
    }

    /** @param array{championMissing: bool, runesReset: bool, droppedItems: list<array{step: int, id: string, name: string}>} $report */
    private function flashImportReport(array $report, string $target): void
    {
        $this->addFlash('success', $this->translator->trans('build.import.done', ['%version%' => $target]));

        if ($report['championMissing']) {
            $this->addFlash('warning', $this->translator->trans('build.import.champion_missing'));
        }
        if ($report['runesReset']) {
            $this->addFlash('warning', $this->translator->trans('build.import.runes_reset'));
        }
        if ($report['droppedItems'] !== []) {
            $names = implode(', ', array_unique(array_map(static fn (array $d): string => $d['name'], $report['droppedItems'])));
            $this->addFlash('warning', $this->translator->trans('build.import.items_dropped', ['%items%' => $names]));
        }
    }

    /** 404 (never 403) when the build does not exist OR belongs to someone else. */
    private function ownedOr404(int $id): Build
    {
        $build = $this->builds->find($id);
        if ($build === null || $build->getOwner()?->getId() !== $this->currentUser()->getId()) {
            throw $this->createNotFoundException('Build not found.');
        }

        return $build;
    }

    /**
     * Build creation is the one write reserved to confirmed accounts (anti-spam
     * of public content). Returns a redirect to bounce unverified users, or null
     * to let the caller proceed.
     */
    private function requireVerifiedEmail(): ?Response
    {
        if ($this->currentUser()->isVerified()) {
            return null;
        }

        $this->addFlash('warning', $this->translator->trans('auth.verify.gate_build'));

        return $this->redirectToRoute('app_builds', status: Response::HTTP_SEE_OTHER);
    }

    /** @return array{name: string, description: ?string, isPublic: bool, structure: null, gameVersion: null, gameMode: string} */
    private static function emptyValues(): array
    {
        return [
            'name' => '',
            'description' => null,
            'isPublic' => false,
            'structure' => null,
            'gameVersion' => null,
            'gameMode' => GameMode::DEFAULT->value,
        ];
    }
}
