<?php
declare(strict_types=1);

namespace App\Service\Build;

use Symfony\Component\HttpFoundation\Request;

/**
 * Immutable snapshot of a build create/update POST: the native fields (name,
 * description, isPublic) plus the island-maintained `structure` JSON, decoded
 * once at the boundary. Carries the raw decoded structure untouched so an
 * invalid submission can be re-proposed to the editor without input loss.
 */
final class BuildSubmission
{
    public const ERROR_NAME_LENGTH = 'build.error.name.length';
    public const ERROR_DESCRIPTION_LENGTH = 'build.error.description.length';

    /** @param array<mixed>|null $structure decoded structure, null when the JSON is unreadable */
    private function __construct(
        public readonly string $name,
        public readonly ?string $description,
        public readonly bool $isPublic,
        public readonly ?array $structure,
    ) {}

    public static function fromRequest(Request $request): self
    {
        $description = trim((string) $request->request->get('description', ''));
        $decoded = json_decode((string) $request->request->get('structure', ''), true);

        return new self(
            name: trim((string) $request->request->get('name', '')),
            description: $description === '' ? null : $description,
            isPublic: $request->request->getBoolean('isPublic'),
            structure: is_array($decoded) ? $decoded : null,
        );
    }

    /**
     * Errors on the native fields + structure decodability. Rune/step/champion
     * semantics belong to {@see BuildStructureValidator} (via the catalog gate).
     *
     * @return list<string> error codes (translation keys)
     */
    public function formErrors(): array
    {
        $errors = [];
        $nameLength = mb_strlen($this->name);
        if ($nameLength < BuildStructureValidator::NAME_MIN || $nameLength > BuildStructureValidator::NAME_MAX) {
            $errors[] = self::ERROR_NAME_LENGTH;
        }
        if ($this->description !== null && mb_strlen($this->description) > BuildStructureValidator::DESCRIPTION_MAX) {
            $errors[] = self::ERROR_DESCRIPTION_LENGTH;
        }
        if ($this->structure === null) {
            $errors[] = BuildStructureValidator::ERROR_STRUCTURE;
        }

        return $errors;
    }
}
