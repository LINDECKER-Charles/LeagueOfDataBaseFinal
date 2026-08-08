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
use App\Service\Build\BuildStructureNormalizer;
use App\Service\Build\BuildSubmission;
use App\Service\Build\BuildViewAssembler;
use App\Service\Audit\Model\AuditAction;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\Model\AuditTarget;
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
 * Authoring of the owner's builds (everything under ^/builds is ROLE_USER by
 * access_control): listing, the editor, create/update and deletion. Ownership
 * failures are 404 — not 403 — so build ids leak no existence oracle.
 *
 * Porting a build to another patch is a separate story with its own services —
 * see {@see BuildImportController}.
 */
final class BuildController extends AbstractPageController
{
    use OwnsBuilds;
    use RendersBuildEditor;
    use ResolvesCurrentUser;

    private const CSRF_SUBMIT = 'submit';
    private const CSRF_DELETE_PREFIX = 'delete-build-';
    private const ERROR_VERSION_UNKNOWN = 'build.error.version.unknown';
    private const ERROR_LANGUAGE_UNKNOWN = 'build.error.language.unknown';

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly BuildRepository $builds,
        private readonly BuildCatalogGate $catalogGate,
        private readonly BuildStructureNormalizer $normalizer,
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
        ['version' => $version, 'lang' => $lang] = $this->pageContext->selection();
        $rows = array_map(
            fn (Build $build): array => ['build' => $build]
                + $this->assembler->listRow($build, $version, $lang),
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

    #[Route(
        '/builds/{id}/edit',
        name: 'app_build_edit',
        requirements: ['id' => '\d+'],
        methods: ['GET'],
    )]
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
            'language' => $build->getLanguage(),
        ]);
    }

    #[Route(
        '/builds/{id}',
        name: 'app_build_update',
        requirements: ['id' => '\d+'],
        methods: ['POST'],
    )]
    public function update(Request $request, int $id): Response
    {
        return $this->handleSubmit($request, $this->ownedOr404($id));
    }

    #[Route(
        '/builds/{id}/delete',
        name: 'app_build_delete',
        requirements: ['id' => '\d+'],
        methods: ['POST'],
    )]
    public function delete(Request $request, int $id): Response
    {
        $build = $this->ownedOr404($id);

        if (!$this->isCsrfTokenValid(
            self::CSRF_DELETE_PREFIX.$id,
            (string) $request->request->get('_token'),
        )) {
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

    /**
     * Shared create/update pipeline: verified email → CSRF → field errors →
     * catalog validation → persist.
     */
    private function handleSubmit(Request $request, ?Build $build): Response
    {
        if ($guard = $this->requireVerifiedEmail()) {
            return $guard;
        }

        ['version' => $version, 'lang' => $lang] = $this->pageContext->selection();
        $submission = BuildSubmission::fromRequest($request, $version, $lang);
        $values = self::submittedValues($submission);
        $token = (string) $request->request->get('_token');

        if (!$this->isCsrfTokenValid(self::CSRF_SUBMIT, $token)) {
            return $this->editorErrorResponse([['build.error.csrf', []]], $build, $values);
        }

        $errors = $this->collectErrors($submission);
        if ($errors !== []) {
            return $this->editorErrorResponse($errors, $build, $values);
        }

        $saved = $this->persistSubmission($build, $submission);
        $this->reportSave($saved, isNew: $build === null);

        return $this->redirectToRoute(
            'app_build_show',
            ['token' => $saved->getShareToken()],
            Response::HTTP_SEE_OTHER,
        );
    }

    /**
     * The submission as the editor's value bag, re-proposed verbatim when the
     * POST is rejected so the author never loses their input.
     *
     * @return array<string, mixed>
     */
    private static function submittedValues(BuildSubmission $submission): array
    {
        return [
            'name' => $submission->name,
            'description' => $submission->description,
            'isPublic' => $submission->isPublic,
            'structure' => $submission->structure,
            'gameVersion' => $submission->gameVersion,
            'gameMode' => ($submission->gameMode ?? GameMode::DEFAULT)->value,
            'language' => $submission->language,
        ];
    }

    /** @return list<array{0: string, 1: array<string, string>}> (code, params) tuples */
    private function collectErrors(BuildSubmission $submission): array
    {
        $isVersionKnown = $this->versionManager->versionExists($submission->gameVersion);
        $errors = $this->metadataErrors($submission, $isVersionKnown);

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

    /**
     * Everything checkable without touching the catalogs: the form's own field
     * errors, plus the target patch and the authoring language.
     *
     * @return list<array{0: string, 1: array<string, string>}>
     */
    private function metadataErrors(BuildSubmission $submission, bool $isVersionKnown): array
    {
        $errors = array_map(
            static fn (string $code): array => [$code, []],
            $submission->formErrors(),
        );
        if (!$isVersionKnown) {
            $errors[] = [self::ERROR_VERSION_UNKNOWN, []];
        }
        // Authoring language is pure metadata (never gates the catalog), but a
        // forged/unknown value is still rejected to keep the filter facet clean.
        if (!$this->versionManager->languageExists($submission->language)) {
            $errors[] = [self::ERROR_LANGUAGE_UNKNOWN, []];
        }

        return $errors;
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
            ->setGameMode($submission->gameMode ?? GameMode::DEFAULT)
            ->setLanguage($submission->language);
        $this->entityManager->flush();

        return $build;
    }

    /** Both trails of a persisted build: the audit journal and the author's flash. */
    private function reportSave(Build $build, bool $isNew): void
    {
        $this->audit->log(
            $isNew ? AuditAction::BuildCreate : AuditAction::BuildUpdate,
            target: AuditTarget::of(AuditTarget::TYPE_BUILD, $build->getId(), $build->getName()),
        );
        $this->addFlash(
            'success',
            $this->translator->trans($isNew ? 'build.flash.created' : 'build.flash.updated'),
        );
    }
}
