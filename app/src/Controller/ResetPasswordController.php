<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\User;
use App\Form\ChangePasswordFormType;
use App\Form\ResetPasswordRequestFormType;
use App\Repository\UserRepository;
use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\AuditTarget;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Mail\AuthMailer;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\PasswordHasher\Hasher\UserPasswordHasherInterface;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Contracts\Translation\TranslatorInterface;
use SymfonyCasts\Bundle\ResetPassword\Controller\ResetPasswordControllerTrait;
use SymfonyCasts\Bundle\ResetPassword\Exception\ResetPasswordExceptionInterface;
use SymfonyCasts\Bundle\ResetPassword\ResetPasswordHelperInterface;

/**
 * Forgot-password flow (SymfonyCasts bundle). Request and check-email are
 * enumeration-safe: both always redirect to the same confirmation page whether
 * or not the address matches an account. Tokens are single-use and short-lived
 * (see reset_password.yaml); the request itself is throttled by the bundle.
 */
final class ResetPasswordController extends AbstractResourceController
{
    use ResetPasswordControllerTrait;

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly ResetPasswordHelperInterface $resetPasswordHelper,
        private readonly UserRepository $users,
        private readonly EntityManagerInterface $entityManager,
        private readonly AuthMailer $authMailer,
        private readonly UserPasswordHasherInterface $passwordHasher,
        private readonly TranslatorInterface $translator,
        private readonly AuditLogger $audit,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/reset-password', name: 'app_forgot_password_request', methods: ['GET', 'POST'])]
    public function request(Request $request): Response
    {
        $form = $this->createForm(ResetPasswordRequestFormType::class);
        $form->handleRequest($request);

        if ($form->isSubmitted() && $form->isValid()) {
            return $this->sendResetEmail((string) $form->get('email')->getData());
        }

        return $this->render('security/reset_password_request.html.twig', [
            'client' => $this->clientData(),
            'requestForm' => $form,
        ]);
    }

    #[Route('/reset-password/check-email', name: 'app_check_email', methods: ['GET'])]
    public function checkEmail(): Response
    {
        // A fake token keeps the page identical for unknown addresses (no oracle).
        $resetToken = $this->getTokenObjectFromSession() ?? $this->resetPasswordHelper->generateFakeResetToken();

        return $this->render('security/reset_password_check_email.html.twig', [
            'client' => $this->clientData(),
            'resetToken' => $resetToken,
        ]);
    }

    #[Route('/reset-password/reset/{token}', name: 'app_reset_password', methods: ['GET', 'POST'])]
    public function reset(Request $request, ?string $token = null): Response
    {
        if ($token !== null) {
            // Move the token out of the URL into the session so it never leaks via Referer.
            $this->storeTokenInSession($token);

            return $this->redirectToRoute('app_reset_password');
        }

        $token = $this->getTokenFromSession();
        if ($token === null) {
            throw $this->createNotFoundException('No reset password token found in the URL or in the session.');
        }

        try {
            $user = $this->resetPasswordHelper->validateTokenAndFetchUser($token);
        } catch (ResetPasswordExceptionInterface) {
            $this->addFlash('error', $this->translator->trans('auth.flash.reset_error'));

            return $this->redirectToRoute('app_forgot_password_request');
        }

        return $this->handleNewPassword($request, $user, $token);
    }

    private function handleNewPassword(Request $request, User $user, string $token): Response
    {
        $form = $this->createForm(ChangePasswordFormType::class);
        $form->handleRequest($request);

        if ($form->isSubmitted() && $form->isValid()) {
            // Invalidate the token before setting the password: single use, even on error after.
            $this->resetPasswordHelper->removeResetRequest($token);
            $user->setPassword($this->passwordHasher->hashPassword($user, (string) $form->get('plainPassword')->getData()));
            $this->entityManager->flush();
            $this->audit->log(AuditAction::UserPasswordReset, target: AuditTarget::user($user));
            $this->cleanSessionAfterReset();

            $this->addFlash('success', $this->translator->trans('auth.flash.reset_success'));

            return $this->redirectToRoute('app_login');
        }

        return $this->render('security/reset_password_reset.html.twig', [
            'client' => $this->clientData(),
            'resetForm' => $form,
        ]);
    }

    /** Always lands on check-email; the token is only created when the address is known. */
    private function sendResetEmail(string $emailFormData): RedirectResponse
    {
        $user = $this->users->findOneBy(['email' => mb_strtolower($emailFormData)]);
        if ($user instanceof User) {
            try {
                $resetToken = $this->resetPasswordHelper->generateResetToken($user);
                $this->authMailer->sendPasswordReset($user, $resetToken);
                $this->setTokenObjectInSession($resetToken);
            } catch (ResetPasswordExceptionInterface) {
                // Throttled or otherwise refused: stay silent so the outcome can't
                // be used to probe which addresses have an account.
            }
        }

        return $this->redirectToRoute('app_check_email');
    }
}
