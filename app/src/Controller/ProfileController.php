<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\User;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Profile\FavoriteSelectionSanitizer;
use App\Service\Profile\FavoriteSlot;
use App\Service\Profile\FavoriteSlots;
use App\Service\Profile\ProfilePresenter;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\PasswordHasher\Hasher\UserPasswordHasherInterface;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Component\Security\Core\Authentication\Token\Storage\TokenStorageInterface;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * The summoner's chamber: profile edition (visibility toggle + four favorite
 * slots) and account deletion (the GDPR erasure right the legal pages announce).
 */
final class ProfileController extends AbstractResourceController
{
    private const CSRF_TOKEN_ID = 'submit';

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly EntityManagerInterface $entityManager,
        private readonly FavoriteSlots $favoriteSlots,
        private readonly FavoriteSelectionSanitizer $sanitizer,
        private readonly ProfilePresenter $presenter,
        private readonly UserPasswordHasherInterface $passwordHasher,
        private readonly TokenStorageInterface $tokenStorage,
        private readonly TranslatorInterface $translator,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/profile', name: 'app_profile', methods: ['GET'])]
    public function index(Request $request): Response
    {
        $user = $this->currentUser();
        ['version' => $version, 'lang' => $lang] = $this->pageContext->selection();

        return $this->render('profile/index.html.twig', [
            'client' => $this->clientData(),
            'user' => $user,
            'maskedEmail' => $this->presenter->maskEmail($user->getEmail()),
            'memberSince' => $this->presenter->memberSince($user->getCreatedAt(), $request->getLocale()),
            'favorites' => $this->favoriteSlots->resolveAll($user, $version, $lang),
            'version' => $version,
            'lang' => $lang,
        ]);
    }

    #[Route('/profile', name: 'app_profile_save', methods: ['POST'])]
    public function save(Request $request): RedirectResponse
    {
        if (!$this->isCsrfTokenValid(self::CSRF_TOKEN_ID, (string) $request->request->get('_token'))) {
            return $this->backToProfileWithError('profile.flash.csrf');
        }

        $user = $this->currentUser();
        ['version' => $version, 'lang' => $lang] = $this->pageContext->selection();

        try {
            $result = $this->sanitizer->sanitize(
                $this->submittedFavorites($request),
                fn (FavoriteSlot $slot, string $id): bool => $this->favoriteSlots->resolve($slot, $id, $version, $lang) !== null,
            );
        } catch (\Throwable $e) {
            // Data layer down: refuse the save rather than silently wiping favorites.
            $this->addFlash('error', $this->dataError(['version' => $version, 'lang' => $lang], $e));

            return $this->redirectToRoute('app_profile', status: Response::HTTP_SEE_OTHER);
        }

        $this->favoriteSlots->apply($user, $result['values']);
        $user->setIsPublicProfile($request->request->getBoolean('isPublicProfile'));
        $this->entityManager->flush();
        $this->flashSaveOutcome($result['invalid']);

        return $this->redirectToRoute('app_profile', status: Response::HTTP_SEE_OTHER);
    }

    #[Route('/profile/delete', name: 'app_profile_delete', methods: ['POST'])]
    public function delete(Request $request): RedirectResponse
    {
        if (!$this->isCsrfTokenValid(self::CSRF_TOKEN_ID, (string) $request->request->get('_token'))) {
            return $this->backToProfileWithError('profile.flash.csrf');
        }

        $user = $this->currentUser();
        if (!$this->passwordHasher->isPasswordValid($user, (string) $request->request->get('password'))) {
            return $this->backToProfileWithError('profile.flash.wrong_password');
        }

        // GDPR erasure: the account row goes away, builds follow via FK cascade.
        $this->entityManager->remove($user);
        $this->entityManager->flush();

        // Kill the authenticated context BEFORE flashing: invalidate() migrates to
        // a fresh empty session, so the farewell flash lands in the new one.
        $this->tokenStorage->setToken(null);
        $request->getSession()->invalidate();
        $this->addFlash('success', $this->translator->trans('profile.flash.deleted'));

        return $this->redirectToRoute('app_home', status: Response::HTTP_SEE_OTHER);
    }

    private function currentUser(): User
    {
        $user = $this->getUser();
        if (!$user instanceof User) {
            // access_control guarantees ROLE_USER; this guards the admin firewall identity.
            throw $this->createAccessDeniedException();
        }

        return $user;
    }

    /** @return array<string, ?string> raw submitted id keyed by slot value */
    private function submittedFavorites(Request $request): array
    {
        $submitted = [];
        foreach (FavoriteSlot::cases() as $slot) {
            $value = $request->request->get($slot->fieldName());
            $submitted[$slot->value] = $value === null ? null : (string) $value;
        }

        return $submitted;
    }

    /** @param list<FavoriteSlot> $invalid */
    private function flashSaveOutcome(array $invalid): void
    {
        foreach ($invalid as $slot) {
            $this->addFlash('warning', $this->translator->trans('profile.flash.favorite_unavailable', [
                '%slot%' => $this->translator->trans('profile.slot.'.$slot->value),
            ]));
        }
        $this->addFlash('success', $this->translator->trans('profile.flash.saved'));
    }

    private function backToProfileWithError(string $messageKey): RedirectResponse
    {
        $this->addFlash('error', $this->translator->trans($messageKey));

        return $this->redirectToRoute('app_profile', status: Response::HTTP_SEE_OTHER);
    }
}
