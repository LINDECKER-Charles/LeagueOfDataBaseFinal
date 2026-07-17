<?php
declare(strict_types=1);

namespace App\Form;

use App\Validator\CnilPassword;
use Symfony\Component\Form\Extension\Core\Type\PasswordType;
use Symfony\Component\Validator\Constraints\Length;
use Symfony\Component\Validator\Constraints\NotBlank;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Single definition of the "new password + confirmation" repeated field, shared
 * by registration and the set-password flow of OAuth-only accounts — the CNIL
 * policy, the DoS length cap and the mismatch message stay in sync everywhere.
 */
final class PasswordFieldOptions
{
    // bcrypt/argon inputs are capped upstream; 4096 guards against DoS-sized payloads.
    public const MAX_LENGTH = 4096;

    /** @return array<string, mixed> options for a RepeatedType field */
    public static function repeated(TranslatorInterface $translator): array
    {
        return [
            'type' => PasswordType::class,
            'mapped' => false,
            // Pre-translated: RepeatedType would look the key up in the `validators`
            // domain, while all project strings live in the `messages` catalogs.
            'invalid_message' => $translator->trans('auth.register.password_mismatch'),
            'first_options' => [
                'label' => 'auth.register.password',
                'attr' => ['autocomplete' => 'new-password'],
            ],
            'second_options' => [
                'label' => 'auth.register.password_confirm',
                'attr' => ['autocomplete' => 'new-password'],
            ],
            'constraints' => [
                new NotBlank(),
                new Length(max: self::MAX_LENGTH),
                new CnilPassword(),
            ],
        ];
    }
}
