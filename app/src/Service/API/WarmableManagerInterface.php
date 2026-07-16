<?php
declare(strict_types=1);

namespace App\Service\API;

use Symfony\Component\DependencyInjection\Attribute\AutoconfigureTag;

/**
 * A DDragon resource manager whose datasets and images can be pre-warmed into
 * object storage ahead of any user request. Implementors are auto-tagged, so a
 * new manager is picked up by {@see \App\Command\WarmupDdragonCommand} and by the
 * streaming loader ({@see \App\Controller\LoaderController}) for free.
 */
#[AutoconfigureTag('app.ddragon.manager')]
interface WarmableManagerInterface
{
    /** DDragon resource type key ('champion', 'item', 'summoner', 'runesReforged'). */
    public function type(): string;

    /**
     * @return array<mixed>
     */
    public function getData(string $version, string $lang): array;

    /**
     * @param array<mixed> $data
     * @return array<mixed>
     */
    public function getImages(string $version, string $lang, bool $force = false, array $data = []): array;

    /**
     * Cost of warming a page slice's images, without fetching anything.
     *
     * @return array{entries: array<string,string>, missing: int}
     *         entries = imageFileName => human-readable display name
     */
    public function collectPlan(string $version, string $lang, int $perPage, int $page): array;

    /**
     * Synchronously fetch + store the still-missing images of a pre-computed
     * entry map, invoking $onStored(string $displayName) as each image lands.
     *
     * @param array<string,string> $entries imageFileName => display name
     */
    public function ingest(string $version, array $entries, callable $onStored): void;
}
