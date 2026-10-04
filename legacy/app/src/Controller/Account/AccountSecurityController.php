<?php
declare(strict_types=1);

namespace App\Controller\Account;

use App\Controller\Concern\BouncesToProfile;
use App\Controller\Concern\ResolvesCurrentUser;
use App\Entity\User;
use App\Form\SetPasswordFormType;
use App\Service\Audit\Model\AuditAction;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\Model\AuditTarget;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Bridge\Doctrine\Validator\Constraints\UniqueEntity;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\PasswordHasher\Hasher\UserPasswordHasherInterface;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Component\Security\Core\Authentication\Token\Storage\TokenStorageInterface;
use Symfony\Component\Validator\ConstraintViolationListInterface;
use Symfony\Component\Validator\Validator\ValidatorInterface;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Everything that changes WHO the account is: summoner identity (username +
 * Riot tagline), password bootstrap for OAuth-only accounts, and the GDPR
 * erasure the legal pages announce.
 *
 * Split from the summoner's chamber ({@see ProfileController} renders it,
 * {@see ProfileCurationController} writes favorites, visibility and the pinned
 * patch) because the two have disjoint reasons to change — password policy and
 * erasure obligations have nothing to do with how a favorite resolves. Every
 * action bounces to /profile, so this controller renders no view.
 */
final class AccountSecurityController extends AbstractController
{
    use BouncesToProfile;
    use ResolvesCurrentUser;

    private const CSRF_TOKEN_ID = 'submit';

    public function __construct(
        private readonly EntityManagerInterface $entityManager,
        private readonly UserPasswordHasherInterface $passwordHasher,
        private readonly TokenStorageInterface $tokenStorage,
        private readonly TranslatorInterface $translator,
        private readonly ValidatorInterface $validator,
        private readonly AuditLogger $audit,
    ) {}

    #[Route('/profile/identity', name: 'app_profile_identity', methods: ['POST'])]
    public function identity(Request $request): RedirectResponse
    {
        if (!$this->isCsrfTokenValid(
            self::CSRF_TOKEN_ID,
            (string) $request->request->get('_token')
        )) {
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
        if (!$this->isCsrfTokenValid(
            self::CSRF_TOKEN_ID,
            (string) $request->request->get('_token')
        )) {
            return $this->backToProfileWithError('profile.flash.csrf');
        }

        $user = $this->currentUser();
        if (!$this->deletionConfirmed($user, $request)) {
            $errorKey = $user->isGoogleAccount()
                ? 'profile.flash.wrong_phrase'
                : 'profile.flash.wrong_password';

            return $this->backToProfileWithError($errorKey);
        }

        $this->erase($user);

        // Kill the authenticated context BEFORE flashing: invalidate() migrates to
        // a fresh empty session, so the farewell flash lands in the new one.
        $this->tokenStorage->setToken(null);
        $request->getSession()->invalidate();
        $this->addFlash('success', $this->translator->trans('profile.flash.deleted'));

        return $this->redirectToRoute('app_home', status: Response::HTTP_SEE_OTHER);
    }

    /**
     * GDPR erasure: the account row goes away, builds follow via FK cascade. The
     * audit target is captured before removal — the id is needed for the line.
     */
    private function erase(User $user): void
    {
        $target = AuditTarget::user($user);
        $this->entityManager->remove($user);
        $this->entityManager->flush();
        $this->audit->log(AuditAction::AccountDelete, target: $target);
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
            || $this->passwordHasher->isPasswordValid(
                $user,
                (string) $request->request->get('password')
            );
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
}
