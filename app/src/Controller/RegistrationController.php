<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\User;
use App\Form\RegistrationFormType;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Bundle\SecurityBundle\Security;
use Symfony\Component\Form\FormInterface;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\PasswordHasher\Hasher\UserPasswordHasherInterface;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Contracts\Translation\TranslatorInterface;

final class RegistrationController extends AbstractResourceController
{
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

        $this->addFlash('success', $this->translator->trans('auth.flash.registered'));

        return $this->redirectToRoute('app_profile');
    }
}
