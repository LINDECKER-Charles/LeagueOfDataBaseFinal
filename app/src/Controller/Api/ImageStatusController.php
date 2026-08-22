<?php
declare(strict_types=1);

namespace App\Controller\Api;

use App\Service\API\Image\ImageStatusResolver;
use App\Service\Client\VersionManager;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Polled by a cold list page to swap its image placeholders for the blobs the
 * deferred ingestion has stored since the render. Read-only by design (see
 * {@see ImageStatusResolver}); `retry=1` marks the client's last attempt, the
 * only one allowed to re-queue work.
 */
final class ImageStatusController extends AbstractController
{
    /** Image file names as Data Dragon ships them (`1001.png`, `perk-images/…/Electrocute.png`). */
    private const NAME_PATTERN = '/^[A-Za-z0-9][A-Za-z0-9_.\/-]{0,199}$/';

    public function __construct(
        private readonly ImageStatusResolver $resolver,
        private readonly VersionManager $versions,
    ) {}

    #[Route(
        '/api/images/{type}',
        name: 'api_image_status',
        methods: ['GET'],
        requirements: ['type' => '[A-Za-z]+'],
    )]
    public function status(string $type, Request $request): JsonResponse
    {
        if (!$this->resolver->knowsType($type)) {
            return $this->failure(Response::HTTP_NOT_FOUND, 'unknown_type');
        }
        $version = trim((string) $request->query->get('version', ''));
        if (!$this->versions->versionExists($version)) {
            return $this->failure(Response::HTTP_BAD_REQUEST, 'unknown_version');
        }
        $names = $this->names((string) $request->query->get('names', ''));
        if ($names === null) {
            return $this->failure(Response::HTTP_BAD_REQUEST, 'invalid_names');
        }

        $status = $this->resolver->status(
            $type,
            $version,
            $names,
            $request->query->getBoolean('retry'),
        );

        // Each answer reflects a moving manifest: never let a shared cache
        // replay a "still pending" to the next reader.
        return new JsonResponse($status, Response::HTTP_OK, ['Cache-Control' => 'no-store']);
    }

    /**
     * The comma-separated `names` query, validated and bounded. Null rejects
     * the whole call: a malformed name is a client bug, not a partial answer.
     *
     * @return ?list<string>
     */
    private function names(string $raw): ?array
    {
        $names = array_values(array_unique(array_filter(
            array_map(trim(...), explode(',', $raw)),
            static fn (string $name): bool => $name !== '',
        )));
        if ($names === [] || \count($names) > ImageStatusResolver::MAX_NAMES_PER_CALL) {
            return null;
        }
        foreach ($names as $name) {
            if (preg_match(self::NAME_PATTERN, $name) !== 1) {
                return null;
            }
        }

        return $names;
    }

    private function failure(int $status, string $error): JsonResponse
    {
        return new JsonResponse(['error' => $error], $status, ['Cache-Control' => 'no-store']);
    }
}
