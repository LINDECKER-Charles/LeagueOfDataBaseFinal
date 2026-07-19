<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Build;

use App\Service\Build\BuildStructureValidator;
use App\Service\Build\BuildSubmission;
use App\Service\Picker\GameMode;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;

/** POST boundary parsing + native-field bounds (name / description / structure JSON / version / mode). */
final class BuildSubmissionTest extends TestCase
{
    private const FALLBACK_VERSION = '16.14.1';
    private const FALLBACK_LANGUAGE = 'en_US';

    private static function submission(array $post): BuildSubmission
    {
        return BuildSubmission::fromRequest(
            Request::create('/builds', 'POST', $post),
            self::FALLBACK_VERSION,
            self::FALLBACK_LANGUAGE,
        );
    }

    public function testParsesAndTrimsFields(): void
    {
        $submission = self::submission([
            'name' => '  Lethality carry  ',
            'description' => '  Snowball early.  ',
            'isPublic' => 'on',
            'structure' => '{"championId":"Aatrox","runes":{},"steps":[]}',
            'game_version' => ' 16.13.1 ',
            'game_mode' => 'aram',
        ]);

        self::assertSame('Lethality carry', $submission->name);
        self::assertSame('Snowball early.', $submission->description);
        self::assertTrue($submission->isPublic);
        self::assertSame('Aatrox', $submission->structure['championId'] ?? null);
        self::assertSame('16.13.1', $submission->gameVersion);
        self::assertSame(GameMode::Aram, $submission->gameMode);
    }

    public function testBlankDescriptionAndMissingCheckboxAreNulled(): void
    {
        $submission = self::submission(['name' => 'ok!', 'description' => '   ']);

        self::assertNull($submission->description);
        self::assertFalse($submission->isPublic);
    }

    public function testMissingVersionAndModeFallBackToTheJsLessDefaults(): void
    {
        $submission = self::submission(['name' => 'valid']);

        self::assertSame(self::FALLBACK_VERSION, $submission->gameVersion);
        self::assertSame(GameMode::DEFAULT, $submission->gameMode);
        self::assertNotContains(BuildSubmission::ERROR_MODE_UNKNOWN, $submission->formErrors());
    }

    public function testLanguageIsTrimmedOrFallsBackToTheContext(): void
    {
        self::assertSame('fr_FR', self::submission(['name' => 'valid', 'language' => '  fr_FR  '])->language);
        self::assertSame(self::FALLBACK_LANGUAGE, self::submission(['name' => 'valid'])->language);
    }

    public function testUnknownModeIsAnExplicitError(): void
    {
        $submission = self::submission(['name' => 'valid', 'structure' => '{"a":1}', 'game_mode' => 'urf']);

        self::assertNull($submission->gameMode);
        self::assertContains(BuildSubmission::ERROR_MODE_UNKNOWN, $submission->formErrors());
    }

    public function testNameBounds(): void
    {
        foreach (['ab' => true, 'abc' => false, str_repeat('n', 80) => false, str_repeat('n', 81) => true] as $name => $shouldFail) {
            $errors = self::submission(['name' => $name, 'structure' => '{"a":1}'])->formErrors();

            self::assertSame($shouldFail, in_array(BuildSubmission::ERROR_NAME_LENGTH, $errors, true), "name: $name");
        }
    }

    public function testDescriptionBound(): void
    {
        $errors = self::submission([
            'name' => 'valid',
            'description' => str_repeat('d', 2001),
            'structure' => '{"a":1}',
        ])->formErrors();

        self::assertContains(BuildSubmission::ERROR_DESCRIPTION_LENGTH, $errors);
    }

    public function testUnreadableStructureJson(): void
    {
        foreach (['', '{oops', '"scalar"'] as $raw) {
            $submission = self::submission(['name' => 'valid', 'structure' => $raw]);

            self::assertNull($submission->structure);
            self::assertContains(BuildStructureValidator::ERROR_STRUCTURE, $submission->formErrors());
        }
    }
}
