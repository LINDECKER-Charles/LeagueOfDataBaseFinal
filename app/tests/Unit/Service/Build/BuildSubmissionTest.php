<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Build;

use App\Service\Build\BuildStructureValidator;
use App\Service\Build\BuildSubmission;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;

/** POST boundary parsing + native-field bounds (name / description / structure JSON). */
final class BuildSubmissionTest extends TestCase
{
    private static function request(array $post): Request
    {
        return Request::create('/builds', 'POST', $post);
    }

    public function testParsesAndTrimsFields(): void
    {
        $submission = BuildSubmission::fromRequest(self::request([
            'name' => '  Lethality carry  ',
            'description' => '  Snowball early.  ',
            'isPublic' => 'on',
            'structure' => '{"championId":"Aatrox","runes":{},"steps":[]}',
        ]));

        self::assertSame('Lethality carry', $submission->name);
        self::assertSame('Snowball early.', $submission->description);
        self::assertTrue($submission->isPublic);
        self::assertSame('Aatrox', $submission->structure['championId'] ?? null);
    }

    public function testBlankDescriptionAndMissingCheckboxAreNulled(): void
    {
        $submission = BuildSubmission::fromRequest(self::request(['name' => 'ok!', 'description' => '   ']));

        self::assertNull($submission->description);
        self::assertFalse($submission->isPublic);
    }

    public function testNameBounds(): void
    {
        foreach (['ab' => true, 'abc' => false, str_repeat('n', 80) => false, str_repeat('n', 81) => true] as $name => $shouldFail) {
            $errors = BuildSubmission::fromRequest(self::request([
                'name' => $name,
                'structure' => '{"a":1}',
            ]))->formErrors();

            self::assertSame($shouldFail, in_array(BuildSubmission::ERROR_NAME_LENGTH, $errors, true), "name: $name");
        }
    }

    public function testDescriptionBound(): void
    {
        $errors = BuildSubmission::fromRequest(self::request([
            'name' => 'valid',
            'description' => str_repeat('d', 2001),
            'structure' => '{"a":1}',
        ]))->formErrors();

        self::assertContains(BuildSubmission::ERROR_DESCRIPTION_LENGTH, $errors);
    }

    public function testUnreadableStructureJson(): void
    {
        foreach (['', '{oops', '"scalar"'] as $raw) {
            $submission = BuildSubmission::fromRequest(self::request(['name' => 'valid', 'structure' => $raw]));

            self::assertNull($submission->structure);
            self::assertContains(BuildStructureValidator::ERROR_STRUCTURE, $submission->formErrors());
        }
    }
}
