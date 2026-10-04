<?php
declare(strict_types=1);

namespace App\Controller\Concern;

use App\Entity\Build;
use App\Service\Picker\GameMode;
use Symfony\Component\HttpFoundation\Response;

/**
 * The build editor view — the happy path and its rejected-submission variant —
 * shared by the two ways of opening it: authoring
 * ({@see \App\Controller\Build\BuildController}) and cross-version import
 * ({@see \App\Controller\Build\BuildImportController}). Both render the same template
 * from the same value bag, so the create/edit mode, the patch pinning and the
 * version choices have exactly one definition.
 *
 * For controllers extending {@see \App\Controller\AbstractPageController} that
 * inject a {@see \Symfony\Contracts\Translation\TranslatorInterface} as `$translator`.
 */
trait RendersBuildEditor
{
    /**
     * Rejected submission: every error is flashed and the editor comes back with
     * the submitted values re-proposed, under a 422 so Turbo swaps the page.
     *
     * @param list<array{0: string, 1: array<string, string>}> $errors
     * @param array<string, mixed> $values
     */
    private function editorErrorResponse(array $errors, ?Build $build, array $values): Response
    {
        foreach ($errors as [$code, $params]) {
            $this->addFlash('error', $this->translator->trans($code, $params));
        }

        return $this->editorResponse($build, $values, Response::HTTP_UNPROCESSABLE_ENTITY);
    }

    /**
     * @param array{name: string, description: ?string, isPublic: bool, structure: ?array<mixed>,
     *               gameVersion: ?string, gameMode: string, language?: ?string} $values
     */
    private function editorResponse(
        ?Build $build,
        array $values,
        int $status = Response::HTTP_OK,
    ): Response {
        ['version' => $version, 'lang' => $lang] = $this->pageContext->selection();
        $selectedVersion = ($values['gameVersion'] ?? '') !== ''
            ? (string) $values['gameVersion']
            : $version;
        // Create/import default the authoring language to the browsing locale;
        // edit carries the build's stored one.
        $selectedLanguage = ($values['language'] ?? '') !== ''
            ? (string) $values['language']
            : $lang;

        return $this->render('build/editor.html.twig', [
            'client' => $this->clientData(),
            'mode' => $build === null ? 'create' : 'edit',
            'build' => $build,
            'values' => $values,
            'version' => $selectedVersion,
            'gameMode' => $values['gameMode'],
            'gameModes' => GameMode::cases(),
            'versionChoices' => $this->versionChoices($selectedVersion),
            'lang' => $lang,
            'language' => $selectedLanguage,
        ], new Response(status: $status));
    }

    /**
     * Every Data Dragon patch for the version select (same full list as the header
     * and profile pickers), plus the currently selected one when it's been delisted
     * upstream, so an old build stays editable pinned to its own patch.
     *
     * @return list<string>
     */
    private function versionChoices(string $selected): array
    {
        $choices = $this->versionManager->getVersions();
        if ($selected !== '' && !in_array($selected, $choices, true)) {
            $choices[] = $selected;
        }

        return $choices;
    }

    /**
     * @return array{
     *     name: string,
     *     description: ?string,
     *     isPublic: bool,
     *     structure: null,
     *     gameVersion: null,
     *     gameMode: string,
     *     language: null
     * }
     */
    private static function emptyValues(): array
    {
        return [
            'name' => '',
            'description' => null,
            'isPublic' => false,
            'structure' => null,
            'gameVersion' => null,
            'gameMode' => GameMode::DEFAULT->value,
            'language' => null,
        ];
    }
}
