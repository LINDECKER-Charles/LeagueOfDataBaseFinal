<?php
declare(strict_types=1);

namespace App\Service\Mail;

use App\Entity\User;
use Symfony\Bridge\Twig\Mime\TemplatedEmail;
use Symfony\Component\Mailer\MailerInterface;
use Symfony\Component\Mime\Address;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;
use Symfony\Contracts\Translation\TranslatorInterface;
use SymfonyCasts\Bundle\ResetPassword\Model\ResetPasswordToken;
use SymfonyCasts\Bundle\VerifyEmail\VerifyEmailHelperInterface;

/**
 * Single owner of the account-lifecycle emails (email confirmation + password
 * reset). Centralises the sender identity, subject translation and the shared
 * Hextech template pair (HTML + text) so the controllers only decide *when* to
 * send, never *how*. Sent synchronously (see messenger.yaml routing).
 */
final readonly class AuthMailer
{
    private const CONFIRM_ROUTE = 'app_verify_email';
    private const RESET_ROUTE = 'app_reset_password';
    private const CONFIRM_SUBJECT = 'email.confirm.subject';
    private const CONFIRM_TEMPLATE = 'email/confirmation';
    private const RESET_SUBJECT = 'email.reset.subject';
    private const RESET_TEMPLATE = 'email/reset_password';

    public function __construct(
        private MailerInterface $mailer,
        private VerifyEmailHelperInterface $verifyEmailHelper,
        private UrlGeneratorInterface $urlGenerator,
        private TranslatorInterface $translator,
        private string $sender,
    ) {
    }

    public function sendEmailConfirmation(User $user): void
    {
        $userId = (string) $user->getId();
        $signature = $this->verifyEmailHelper->generateSignature(
            self::CONFIRM_ROUTE,
            $userId,
            $user->getEmail(),
            ['id' => $userId],
        );

        $this->send($user, self::CONFIRM_SUBJECT, self::CONFIRM_TEMPLATE, [
            'actionUrl' => $signature->getSignedUrl(),
            'expiresAtMessageKey' => $signature->getExpirationMessageKey(),
            'expiresAtMessageData' => $signature->getExpirationMessageData(),
        ]);
    }

    public function sendPasswordReset(User $user, ResetPasswordToken $token): void
    {
        $actionUrl = $this->urlGenerator->generate(
            self::RESET_ROUTE,
            ['token' => $token->getToken()],
            UrlGeneratorInterface::ABSOLUTE_URL,
        );

        $this->send($user, self::RESET_SUBJECT, self::RESET_TEMPLATE, [
            'actionUrl' => $actionUrl,
            'expiresAtMessageKey' => $token->getExpirationMessageKey(),
            'expiresAtMessageData' => $token->getExpirationMessageData(),
        ]);
    }

    /** @param array<string, mixed> $context */
    private function send(User $user, string $subjectKey, string $template, array $context): void
    {
        $email = (new TemplatedEmail())
            ->from(Address::create($this->sender))
            ->to(new Address($user->getEmail(), $user->displayName()))
            ->subject($this->translator->trans($subjectKey))
            ->htmlTemplate($template.'.html.twig')
            ->textTemplate($template.'.txt.twig')
            ->context($context + ['user' => $user]);

        $this->mailer->send($email);
    }
}
