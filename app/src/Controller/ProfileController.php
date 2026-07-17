<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\User;
use App\Form\SetPasswordFormType;
use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\AuditTarget;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Profile\ChampionSkins;
use App\Service\Profile\FavoriteSelectionSanitizer;
use App\Service\Profile\FavoriteSlot;
use App\Service\Profile\FavoriteSlots;
use App\Service\Profile\ProfilePresenter;
use App\Service\Profile\PublicProfileView;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Bridge\Doctrine\Validator\Constraints\UniqueEntity;
use Symfony\Component\Form\FormInterface;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\PasswordHasher\Hasher\UserPasswordHasherInterface;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Component\Security\Core\Authentication\Token\Storage\TokenStorageInterface;
use Symfony\Component\Validator\ConstraintViolationListInterface;
use Symfony\Component\Validator\Validator\ValidatorInterface;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * The summoner's chamber: profile edition (visibility toggle + four favorite
 * slots), summoner identity (username + Riot tagline), password bootstrap for
 * OAuth-only accounts, and account deletion (the GDPR erasure right the legal
 * pages announce).
 */
final class ProfileController extends AbstractResourceController
{
    private const CSRF_TOKEN_ID = 'submit';
    private const SKIN_ID_MAX_LENGTH = 64;

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly EntityManagerInterface $entityManager,
        private readonly FavoriteSlots $favoriteSlots,
        private readonly FavoriteSelectionSanitizer $sanitizer,
        private readonly ChampionSkins $skins,
        private readonly PublicProfileView $publicView,
        private readonly ProfilePresenter $presenter,
        private readonly UserPasswordHasherInterface $passwordHasher,
        private readonly TokenStorageInterface $tokenStorage,
        private readonly TranslatorInterface $translator,
        private readonly ValidatorInterface $validator,
        private readonly AuditLogger $audit,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/profile', name: 'app_profile', methods: ['GET'])]
    public function index(Request $request): Response
    {
        $user = $this->currentUser();
        ['version' => $version, 'lang' => $lang] = $this->pageContext->selection();
        $skinBanner = $this->skins->resolveBanner($user->getFavoriteSkinId(), $version, $lang);

        return $this->render('profile/index.html.twig', [
            'client' => $this->clientData(),
            'user' => $user,
            'maskedEmail' => $this->presenter->maskEmail($user->getEmail()),
            'memberSince' => $this->presenter->memberSince($user->getCreatedAt(), $request->getLocale()),
            'favorites' => $this->favoriteSlots->resolveAll($user, $version, $lang),
            'skinBanner' => $skinBanner,
            'heroBackground' => $this->skins->heroBackground($skinBanner, $user->getFavoriteChampionId()),
            'version' => $version,
            'lang' => $lang,
            // OAuth-only accounts get the "set a password" panel.
            'passwordForm' => $user->hasPassword() ? null : $this->createForm(SetPasswordFormType::class),
        ]);
    }

    #[Route('/profile/preview', name: 'app_profile_preview', methods: ['GET'])]
    public function preview(Request $request): Response
    {
        $user = $this->currentUser();
        ['version' => $version, 'lang' => $lang] = $this->pageContext->selection();

        // The owner sees their own public card verbatim — even while private —
        // rendered from the same builder the /u/{username} route uses.
        return $this->render('profile/public.html.twig', [
            'client' => $this->clientData(),
            'preview' => true,
        ] + $this->publicView->build($user, $version, $lang, $request->getLocale()));
    }

    #[Route('/profile', name: 'app_profile_save', methods: ['POST'])]
    public function save(Request $request): Response
    {
        // The favorites panel auto-saves via fetch (XHR → JSON); a no-JS submit
        // keeps the flash + redirect round-trip.
        $isXhr = $request->isXmlHttpRequest();

        if (!$this->isCsrfTokenValid(self::CSRF_TOKEN_ID, (string) $request->request->get('_token'))) {
            return $isXhr
                ? new JsonResponse(['ok' => false, 'error' => $this->translator->trans('profile.flash.csrf')], Response::HTTP_FORBIDDEN)
                : $this->backToProfileWithError('profile.flash.csrf');
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
            $message = $this->dataError(['version' => $version, 'lang' => $lang], $e);
            if ($isXhr) {
                return new JsonResponse(['ok' => false, 'error' => $message], Response::HTTP_SERVICE_UNAVAILABLE);
            }
            $this->addFlash('error', $message);

            return $this->redirectToRoute('app_profile', status: Response::HTTP_SEE_OTHER);
        }

        $skin = $this->sanitizeSkinFavorite($request);
        $this->favoriteSlots->apply($user, $result['values']);
        $user->setFavoriteSkinId($skin['value']);
        $user->setIsPublicProfile($request->request->getBoolean('isPublicProfile'));
        $this->entityManager->flush();
        $this->audit->log(AuditAction::ProfileUpdate, metadata: ['section' => 'favorites']);

        if ($isXhr) {
            return new JsonResponse([
                'ok' => true,
                'invalidFavorites' => array_map(static fn (FavoriteSlot $slot): string => $slot->value, $result['invalid']),
                'skinInvalid' => $skin['invalid'],
                'isPublicProfile' => $user->isPublicProfile(),
            ]);
        }

        $this->flashSaveOutcome($result['invalid'], $skin['invalid']);

        return $this->redirectToRoute('app_profile', status: Response::HTTP_SEE_OTHER);
    }

    #[Route('/profile/identity', name: 'app_profile_identity', methods: ['POST'])]
    public function identity(Request $request): RedirectResponse
    {
        if (!$this->isCsrfTokenValid(self::CSRF_TOKEN_ID, (string) $request->request->get('_token'))) {
            return $this->backToProfileWithError('profile.flash.csrf');
        }

        $user = $this->currentUser();
        $tagline = trim((string) $request->request->get('riotTagline'));
        $user->setUsername(trim((string) $request->request->get('username')));
        $user->setRiotTagline($tagline === '' ? null : $tagline);

        $violations = $this->validator->validate($user);
        if (\count($violations) > 0) {
            // Discard the invalid mutation so a later flush can never persist it.
            $this->entityManager->refresh($user);

            return $this->backToProfileWithError($this->identityErrorKey($violations));
        }

        $this->entityManager->flush();
        $this->audit->log(AuditAction::ProfileUpdate, metadata: ['section' => 'identity']);
        $this->addFlash('success', $this->translator->trans('profile.flash.identity_saved'));

        return $this->redirectToRoute('app_profile', status: Response::HTTP_SEE_OTHER);
    }

    #[Route('/profile/password', name: 'app_profile_password', methods: ['POST'])]
    public function setPassword(Request $request): RedirectResponse
    {
        $user = $this->currentUser();
        if ($user->hasPassword()) {
            // Replacing an existing password would need current-password re-auth — out of scope.
            return $this->backToProfileWithError('profile.flash.password_exists');
        }

        $form = $this->createForm(SetPasswordFormType::class);
        $form->handleRequest($request);
        if (!$form->isSubmitted() || !$form->isValid()) {
            return $this->backToProfileWithFormErrors($form);
        }

        $plainPassword = (string) $form->get('plainPassword')->getData();
        $user->setPassword($this->passwordHasher->hashPassword($user, $plainPassword));
        $this->entityManager->flush();
        $this->audit->log(AuditAction::ProfileUpdate, metadata: ['section' => 'password']);
        $this->addFlash('success', $this->translator->trans('profile.flash.password_set'));

        return $this->redirectToRoute('app_profile', status: Response::HTTP_SEE_OTHER);
    }

    #[Route('/profile/delete', name: 'app_profile_delete', methods: ['POST'])]
    public function delete(Request $request): RedirectResponse
    {
        if (!$this->isCsrfTokenValid(self::CSRF_TOKEN_ID, (string) $request->request->get('_token'))) {
            return $this->backToProfileWithError('profile.flash.csrf');
        }

        $user = $this->currentUser();
        if (!$this->deletionConfirmed($user, $request)) {
            $errorKey = $user->isGoogleAccount() ? 'profile.flash.wrong_phrase' : 'profile.flash.wrong_password';

            return $this->backToProfileWithError($errorKey);
        }

        // GDPR erasure: the account row goes away, builds follow via FK cascade.
        // Capture the target before removal — the id is needed for the audit line.
        $target = AuditTarget::user($user);
        $this->entityManager->remove($user);
        $this->entityManager->flush();
        $this->audit->log(AuditAction::AccountDelete, target: $target);

        // Kill the authenticated context BEFORE flashing: invalidate() migrates to
        // a fresh empty session, so the farewell flash lands in the new one.
        $this->tokenStorage->setToken(null);
        $request->getSession()->invalidate();
        $this->addFlash('success', $this->translator->trans('profile.flash.deleted'));

        return $this->redirectToRoute('app_home', status: Response::HTTP_SEE_OTHER);
    }

    /**
     * Google accounts confirm erasure by typing a locale-aware phrase — their credential
     * is the OAuth `sub`, so a password prompt is either impossible or off-identity. Classic
     * accounts confirm with their password; one without any password stays deletable on CSRF
     * alone so the GDPR erasure right is never blocked.
     */
    private function deletionConfirmed(User $user, Request $request): bool
    {
        if ($user->isGoogleAccount()) {
            $expected = trim($this->translator->trans('profile.danger.confirm_phrase'));
            $typed = trim((string) $request->request->get('confirmation'));

            return $typed !== '' && mb_strtolower($typed) === mb_strtolower($expected);
        }

        return !$user->hasPassword()
            || $this->passwordHasher->isPasswordValid($user, (string) $request->request->get('password'));
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

    /**
     * Skin banner favorite: art is CDN-hotlinked and self-validating (a bad id
     * 404s and the hero's onerror hides it), so a format + length gate is enough
     * — no data-layer existence check, which keeps the save outage-proof.
     *
     * @return array{value: ?string, invalid: bool}
     */
    private function sanitizeSkinFavorite(Request $request): array
    {
        $raw = trim((string) $request->request->get('favoriteSkinId', ''));
        if ($raw === '') {
            return ['value' => null, 'invalid' => false];
        }
        if (mb_strlen($raw) > self::SKIN_ID_MAX_LENGTH || !$this->skins->isWellFormed($raw)) {
            return ['value' => null, 'invalid' => true];
        }

        return ['value' => $raw, 'invalid' => false];
    }

    /** @param list<FavoriteSlot> $invalid */
    private function flashSaveOutcome(array $invalid, bool $skinInvalid): void
    {
        foreach ($invalid as $slot) {
            $this->addFlash('warning', $this->translator->trans('profile.flash.favorite_unavailable', [
                '%slot%' => $this->translator->trans('profile.slot.'.$slot->value),
            ]));
        }
        if ($skinInvalid) {
            $this->addFlash('warning', $this->translator->trans('profile.flash.skin_unavailable'));
        }
        $this->addFlash('success', $this->translator->trans('profile.flash.saved'));
    }

    /** Map the first identity violation to a player-facing message key. */
    private function identityErrorKey(ConstraintViolationListInterface $violations): string
    {
        $violation = $violations->get(0);
        if ($violation->getPropertyPath() === 'riotTagline') {
            return 'profile.identity.tag_invalid';
        }

        return $violation->getConstraint() instanceof UniqueEntity
            ? 'profile.identity.username_taken'
            : 'profile.identity.username_invalid';
    }

    private function backToProfileWithError(string $messageKey): RedirectResponse
    {
        $this->addFlash('error', $this->translator->trans($messageKey));

        return $this->redirectToRoute('app_profile', status: Response::HTTP_SEE_OTHER);
    }

    /** Form errors are already translated (messages domain, see PasswordFieldOptions). */
    private function backToProfileWithFormErrors(FormInterface $form): RedirectResponse
    {
        $hasFlashed = false;
        foreach ($form->getErrors(true) as $error) {
            $this->addFlash('error', $error->getMessage());
            $hasFlashed = true;
        }
        if (!$hasFlashed) {
            $this->addFlash('error', $this->translator->trans('profile.flash.csrf'));
        }

        return $this->redirectToRoute('app_profile', status: Response::HTTP_SEE_OTHER);
    }
}
