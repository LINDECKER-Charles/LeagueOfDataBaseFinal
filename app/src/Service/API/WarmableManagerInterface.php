<?php
declare(strict_types=1);

namespace App\Service\API;

use Symfony\Component\DependencyInjection\Attribute\AutoconfigureTag;

/**
 * A DDragon resource manager whose datasets and images can be pre-warmed into
 * object storage ahead of any user request. Implementors are auto-tagged, so a
 * new manager is picked up by {@see \App\Command\WarmupDdragonCommand} for free.
 */
#[AutoconfigureTag('app.ddragon.manager')]
interface WarmableManagerInterface
{
    /**
     * @return array<mixed>
     */
    public function getData(string $version, string $lang): array;

    /**
     * @param array<mixed> $data
     * @return array<mixed>
     */
    public function getImages(string $version, string $lang, bool $force = false, array $data = []): array;
}
