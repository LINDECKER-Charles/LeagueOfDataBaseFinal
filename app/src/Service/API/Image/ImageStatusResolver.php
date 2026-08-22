<?php
declare(strict_types=1);

namespace App\Service\API\Image;

use App\Service\Storage\WebpSibling;
use Symfony\Component\DependencyInjection\Attribute\AutowireIterator;

/**
 * Resolves the in-page image refresh of a cold list: which of the requested
 * image names landed in object storage since the page was rendered. A manifest
 * read, never a fetch — except on the caller's last attempt, where the missing
 * names are re-queued for after the response ({@see ImageStatusInterface::warmLater}).
 */
final class ImageStatusResolver
{
    /**
     * Names per call — one grid page of cards plus headroom. Mirrored client-side
     * through the `image_status_batch()` Twig function, never retyped.
     */
    public const MAX_NAMES_PER_CALL = 48;

    /** @var array<string,ImageStatusInterface> resource type => manager */
    private readonly array $managers;

    /** @param iterable<object> $managers */
    public function __construct(
        #[AutowireIterator('app.ddragon.manager')]
        iterable $managers,
    ) {
        $byType = [];
        foreach ($managers as $manager) {
            if ($manager instanceof ImageStatusInterface) {
                $byType[$manager->type()] = $manager;
            }
        }
        $this->managers = $byType;
    }

    public function knowsType(string $type): bool
    {
        return isset($this->managers[$type]);
    }

    /**
     * @param string[] $names
     * @return array{
     *     images: array<string, ?array{src: string, webp: ?string}>,
     *     pending: list<string>
     * } settled name => browser-ready paths (null = definitive absence)
     */
    public function status(string $type, string $version, array $names, bool $isLastAttempt): array
    {
        $manager = $this->managers[$type];
        $status  = $manager->manifestStatus($version, $names);

        if ($isLastAttempt && $status['pending'] !== []) {
            $manager->warmLater($version, $status['pending']);
        }

        return [
            'images'  => array_map(self::candidates(...), $status['images']),
            'pending' => $status['pending'],
        ];
    }

    /** @return ?array{src: string, webp: ?string} */
    private static function candidates(?string $path): ?array
    {
        if ($path === null) {
            return null;
        }

        return ['src' => '/'.$path, 'webp' => WebpSibling::of('/'.$path)];
    }
}
