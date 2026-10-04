<?php
declare(strict_types=1);

namespace App\Validator;

use Symfony\Component\Validator\Constraint;
use Symfony\Component\Validator\ConstraintValidator;
use Symfony\Component\Validator\Exception\UnexpectedTypeException;
use Symfony\Component\Validator\Exception\UnexpectedValueException;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * One violation per unmet CNIL criterion, so the register form lists exactly
 * what is missing. "Special" is deliberately wide: any character that is
 * neither a letter nor a digit counts (all ASCII punctuation/symbols, spaces,
 * any Unicode symbol) — mirrored client-side by assets/vue/security/passwordRules.ts.
 */
final class CnilPasswordValidator extends ConstraintValidator
{
    private const DENYLIST_PATH = __DIR__.'/Resources/common-passwords.txt';

    /** @var array<string, true>|null lazily loaded lookup table (one instance per process) */
    private ?array $denylist = null;

    public function __construct(private readonly TranslatorInterface $translator)
    {
    }

    public function validate(mixed $value, Constraint $constraint): void
    {
        if (!$constraint instanceof CnilPassword) {
            throw new UnexpectedTypeException($constraint, CnilPassword::class);
        }
        if ($value === null || $value === '') {
            return; // emptiness is NotBlank's concern
        }
        if (!\is_string($value)) {
            throw new UnexpectedValueException($value, 'string');
        }

        foreach ($this->failedRules($value, $constraint) as $messageKey) {
            // Pre-translated from the `messages` domain (see CnilPassword docblock).
            $this->context->buildViolation($this->translator->trans($messageKey))->addViolation();
        }
    }

    /** @return list<string> message keys of the unmet criteria */
    private function failedRules(string $password, CnilPassword $constraint): array
    {
        $checks = [
            $constraint->tooShort => mb_strlen($password) >= CnilPassword::MIN_LENGTH,
            $constraint->missingLowercase => preg_match('/\p{Ll}/u', $password) === 1,
            $constraint->missingUppercase => preg_match('/\p{Lu}/u', $password) === 1,
            $constraint->missingDigit => preg_match('/[0-9]/', $password) === 1,
            $constraint->missingSpecial => preg_match('/[^\p{L}\p{N}]/u', $password) === 1,
            $constraint->tooCommon => !$this->isCommonPassword($password),
        ];

        return array_keys(array_filter($checks, static fn (bool $satisfied): bool => !$satisfied));
    }

    private function isCommonPassword(string $password): bool
    {
        if ($this->denylist === null) {
            $lines = file(self::DENYLIST_PATH, \FILE_IGNORE_NEW_LINES | \FILE_SKIP_EMPTY_LINES);
            $this->denylist = array_fill_keys($lines === false ? [] : $lines, true);
        }

        return isset($this->denylist[mb_strtolower($password)]);
    }
}
