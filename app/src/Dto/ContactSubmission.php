<?php
declare(strict_types=1);

namespace App\Dto;

use App\Entity\Enum\ContactCategory;
use App\Entity\ContactMessage;
use Symfony\Component\Validator\Constraints as Assert;

/**
 * Validated payload of the footer contact form. Immutable: the controller maps
 * the raw request into it, validates it, then hands it to
 * {@see \App\Entity\ContactMessage}. Violation messages are translation keys
 * ({@see translations/messages.*.yaml}, `contact.error.*`).
 *
 * The honeypot lives on the request, not here — a bot-filled decoy never reaches
 * this object (the controller short-circuits before construction).
 */
final readonly class ContactSubmission
{
    public function __construct(
        #[Assert\NotNull(message: 'contact.error.category')]
        public ?ContactCategory $category,

        #[Assert\NotBlank(message: 'contact.error.email')]
        #[Assert\Email(message: 'contact.error.email')]
        #[Assert\Length(max: ContactMessage::EMAIL_MAX_LENGTH)]
        public string $email,

        #[Assert\NotBlank(message: 'contact.error.message')]
        #[Assert\Length(
            min: 10,
            max: 5000,
            minMessage: 'contact.error.message_short',
            maxMessage: 'contact.error.message_long',
        )]
        public string $message,

        #[Assert\Length(max: ContactMessage::NAME_MAX_LENGTH)]
        public ?string $name = null,

        #[Assert\Length(max: ContactMessage::SUBJECT_MAX_LENGTH)]
        public ?string $subject = null,

        public ?string $locale = null,
    ) {}
}
