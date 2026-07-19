<?php
declare(strict_types=1);

namespace App\Controller;

use App\Controller\Concern\ThrottlesByIp;
use App\Entity\User;
use App\Form\RegistrationFormType;
use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\AuditTarget;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Mail\AuthMailer;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Bundle\SecurityBundle\Security;
use Symfony\Component\Form\FormInterface;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Mailer\Exception\TransportExceptionInterface;
use Symfony\Component\PasswordHasher\Hasher\UserPasswordHasherInterface;
use Symfony\Component\RateLimiter\RateLimiterFactoryInterface;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Contracts\Translation\TranslatorInterface;

final class RegistrationController extends AbstractResourceController
{
    use ThrottlesByIp;

    private const FIREWALL_NAME = 'main';
    private const AUTHENTICATOR = 'form_login';

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly EntityManagerInterface $entityManager,
        private readonly UserPasswordHasherInterface $passwordHasher,
        private readonly Security $security,
        private readonly TranslatorInterface $translator,
        private readonly AuthMailer $authMailer,
        private readonly AuditLogger $audit,
        private readonly RateLimiterFactoryInterface $registrationLimiter,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/register', name: 'app_register', methods: ['GET', 'POST'])]
    public function register(Request $request): Response
    {
        if ($this->getUser() instanceof User) {
            return $this->redirectToRoute('app_profile');
        }

        $user = new User();
        $form = $this->createForm(RegistrationFormType::class, $user);
        $form->handleRequest($request);

        if ($form->isSubmitted() && $this->isRateLimited($this->registrationLimiter, $request)) {
            $this->addFlash('error', $this->translator->trans('auth.flash.too_many_attempts'));

            return $this->redirectToRoute('app_register');
        }
        if ($form->isSubmitted() && $form->isValid()) {
            return $this->finalizeRegistration($user, $form);
        }

        return $this->render('security/register.html.twig', [
            'client' => $this->clientData(),
            'registrationForm' => $form,
        ]);
    }

    /** Hash + persist the new account, then log it in on the main firewall. */
    private function finalizeRegistration(User $user, FormInterface $form): Response
    {
        $plainPassword = (string) $form->get('plainPassword')->getData();
        $user->setPassword($this->passwordHasher->hashPassword($user, $plainPassword));

        $this->entityManager->persist($user);
        $this->entityManager->flush();

        $this->security->login($user, self::AUTHENTICATOR, self::FIREWALL_NAME);
        $this->audit->log(AuditAction::UserRegister, target: AuditTarget::user($user));

        $this->addFlash('success', $this->translator->trans('auth.flash.registered'));
        $this->sendConfirmation($user);

        return $this->redirectToRoute('app_profile');
    }

    /**
     * Fire-and-forget: a failed send must not fail the sign-up — the account
     * exists and the persistent banner offers a one-click resend.
     */
    private function sendConfirmation(User $user): void
    {
        try {
            $this->authMailer->sendEmailConfirmation($user);
            $this->addFlash('info', $this->translator->trans('auth.flash.verify_sent'));
        } catch (TransportExceptionInterface) {
            // Swallowed on purpose (see docblock); recoverable from the banner.
        }
    }
}
