<?php
declare(strict_types=1);

namespace App\Tests\Unit\Dto;

use App\Dto\ContactSubmission;
use App\Entity\ContactMessage;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;
use Symfony\Component\Validator\Validation;
use Symfony\Component\Validator\Validator\ValidatorInterface;

/**
 * The form bounds must never be looser than the columns that store them: a
 * payload accepted here is flushed verbatim into {@see ContactMessage}, so any
 * drift would surface as an SQL truncation error after a successful validation.
 */
final class ContactSubmissionTest extends TestCase
{
    private ValidatorInterface $validator;

    protected function setUp(): void
    {
        $this->validator = Validation::createValidatorBuilder()
            ->enableAttributeMapping()
            ->getValidator();
    }

    #[DataProvider('provideStoredTextFields')]
    public function testFieldAcceptsExactlyItsColumnLength(string $property, int $maxLength): void
    {
        self::assertCount(
            0,
            $this->validator->validatePropertyValue(
                ContactSubmission::class,
                $property,
                str_repeat('a', $maxLength),
            ),
        );
    }

    #[DataProvider('provideStoredTextFields')]
    public function testFieldRejectsOneCharOverItsColumnLength(string $property, int $max): void
    {
        self::assertGreaterThan(
            0,
            \count($this->validator->validatePropertyValue(
                ContactSubmission::class,
                $property,
                str_repeat('a', $max + 1),
            )),
        );
    }

    /** @return iterable<string, array{string, int}> */
    public static function provideStoredTextFields(): iterable
    {
        yield 'name' => ['name', ContactMessage::NAME_MAX_LENGTH];
        yield 'subject' => ['subject', ContactMessage::SUBJECT_MAX_LENGTH];
    }
}
