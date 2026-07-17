<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\Build;
use App\Entity\User;
use App\Repository\BuildRepository;
use App\Service\Build\BuildCatalogGate;
use App\Service\Build\BuildStructureNormalizer;
use App\Service\Build\BuildSubmission;
use App\Service\Build\BuildViewAssembler;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
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
    private const CSRF_SUBMIT = 'submit';
    private const CSRF_DELETE_PREFIX = 'delete-build-';

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
        return $this->editorResponse(null, self::emptyValues());
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
        ]);
    }

    #[Route('/builds/{id}', name: 'app_build_update', requirements: ['id' => '\d+'], methods: ['POST'])]
    public function update(Request $request, int $id): Response
    {
        return $this->handleSubmit($request, $this->ownedOr404($id));
    }

    #[Route('/builds/{id}/delete', name: 'app_build_delete', requirements: ['id' => '\d+'], methods: ['POST'])]
    public function delete(Request $request, int $id): Response
    {
        $build = $this->ownedOr404($id);

        if (!$this->isCsrfTokenValid(self::CSRF_DELETE_PREFIX.$id, (string) $request->request->get('_token'))) {
            $this->addFlash('error', $this->translator->trans('build.error.csrf'));

            return $this->redirectToRoute('app_builds', status: Response::HTTP_SEE_OTHER);
        }

        $this->entityManager->remove($build);
        $this->entityManager->flush();
        $this->addFlash('success', $this->translator->trans('build.flash.deleted'));

        return $this->redirectToRoute('app_builds', status: Response::HTTP_SEE_OTHER);
    }

    /** Shared create/update pipeline: CSRF → field errors → catalog validation → persist. */
    private function handleSubmit(Request $request, ?Build $build): Response
    {
        $submission = BuildSubmission::fromRequest($request);
        $values = [
            'name' => $submission->name,
            'description' => $submission->description,
            'isPublic' => $submission->isPublic,
            'structure' => $submission->structure,
        ];

        if (!$this->isCsrfTokenValid(self::CSRF_SUBMIT, (string) $request->request->get('_token'))) {
            return $this->editorErrorResponse(['build.error.csrf'], $build, $values);
        }

        $errors = $this->collectErrors($submission);
        if ($errors !== []) {
            return $this->editorErrorResponse($errors, $build, $values);
        }

        $isNew = $build === null;
        $build = $this->persistSubmission($build, $submission);
        $this->addFlash('success', $this->translator->trans($isNew ? 'build.flash.created' : 'build.flash.updated'));

        return $this->redirectToRoute(
            'app_build_show',
            ['token' => $build->getShareToken()],
            Response::HTTP_SEE_OTHER,
        );
    }

    /** @return list<string> */
    private function collectErrors(BuildSubmission $submission): array
    {
        $errors = $submission->formErrors();
        if ($submission->structure === null) {
            return $errors;
        }

        $sel = $this->pageContext->selection();
        try {
            return [...$errors, ...$this->catalogGate->validate($submission->structure, $sel['version'], $sel['lang'])];
        } catch (\Throwable) {
            // Transient catalog outage: refuse the write honestly rather than
            // accepting an unverified structure or 500ing away the user's input.
            return [...$errors, 'build.error.catalog_unavailable'];
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
            // The structure was just re-validated against the CURRENT catalogs,
            // so the build is now written on the current patch — update included.
            ->setGameVersion($this->pageContext->selection()['version']);
        $this->entityManager->flush();

        return $build;
    }

    /** @param list<string> $codes */
    private function editorErrorResponse(array $codes, ?Build $build, array $values): Response
    {
        foreach ($codes as $code) {
            $this->addFlash('error', $this->translator->trans($code));
        }

        return $this->editorResponse($build, $values, Response::HTTP_UNPROCESSABLE_ENTITY);
    }

    /**
     * @param array{name: string, description: ?string, isPublic: bool, structure: ?array<mixed>} $values
     */
    private function editorResponse(?Build $build, array $values, int $status = Response::HTTP_OK): Response
    {
        $sel = $this->pageContext->selection();

        return $this->render('build/editor.html.twig', [
            'client' => $this->clientData(),
            'mode' => $build === null ? 'create' : 'edit',
            'build' => $build,
            'values' => $values,
            'version' => $sel['version'],
            'lang' => $sel['lang'],
        ], new Response(status: $status));
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

    private function currentUser(): User
    {
        $user = $this->getUser();
        if (!$user instanceof User) {
            // access_control guarantees ROLE_USER here; guard against misconfig.
            throw $this->createAccessDeniedException();
        }

        return $user;
    }

    /** @return array{name: string, description: ?string, isPublic: bool, structure: null} */
    private static function emptyValues(): array
    {
        return ['name' => '', 'description' => null, 'isPublic' => false, 'structure' => null];
    }
}
