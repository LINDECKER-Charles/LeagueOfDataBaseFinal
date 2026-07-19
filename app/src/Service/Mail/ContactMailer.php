<?php
declare(strict_types=1);

namespace App\Service\Mail;

use App\Entity\ContactMessage;
use Symfony\Bridge\Twig\Mime\TemplatedEmail;
use Symfony\Component\Mailer\MailerInterface;
use Symfony\Component\Mime\Address;

/**
 * Notifies the operator mailbox (CONTACT_RECIPIENT) of a new footer submission.
 * The envelope From stays the authenticated sender identity (MAILER_FROM, whose
 * domain SPF/DKIM authorises the relay); the visitor's address is set as Reply-To
 * so a one-click reply reaches them. Sent synchronously — no worker is deployed
 * (see messenger.yaml). An empty recipient disables the feature cleanly: the
 * message is still persisted for the /admin inbox, just not emailed.
 */
final readonly class ContactMailer
{
    private const TEMPLATE = 'email/contact_notification';

    public function __construct(
        private MailerInterface $mailer,
        private string $sender,
        private string $recipient,
    ) {}

    public function isEnabled(): bool
    {
        return $this->recipient !== '';
    }

    public function sendNotification(ContactMessage $message): void
    {
        if (!$this->isEnabled()) {
            return;
        }

        $email = (new TemplatedEmail())
            ->from(Address::create($this->sender))
            ->to(new Address($this->recipient))
            ->replyTo(new Address($message->getEmail(), $message->getName() ?? ''))
            ->subject($this->subject($message))
            ->htmlTemplate(self::TEMPLATE.'.html.twig')
            ->textTemplate(self::TEMPLATE.'.txt.twig')
            ->context(['message' => $message]);

        $this->mailer->send($email);
    }

    private function subject(ContactMessage $message): string
    {
        $tail = $message->getSubject() ?? $message->getName() ?? $message->getEmail();

        return sprintf('[Contact · %s] %s', $message->getCategory()->label(), $tail);
    }
}
