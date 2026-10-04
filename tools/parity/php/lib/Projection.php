<?php
declare(strict_types=1);

namespace LoDbParity;

use App\Service\API\DatasetRef;

/**
 * One (version, language) document, in the shape of the new stack's `catalog export`:
 * `version`, `language`, then each resource as `{contentLanguage, entries}`.
 *
 * Fields the legacy stack has no notion of (the canonical `path` of ADR 0005) are left out;
 * a champion whose detail page was never visited carries `detail: false` and no
 * passive, spells or skins.
 */
final class Projection
{
    /** Export key => [legacy dataset type, projection class]. */
    private const RESOURCES = [
        'champions' => ['champion', ChampionProjection::class],
        'items'     => ['item', ItemProjection::class],
        'runes'     => ['runesReforged', RuneProjection::class],
        'summoners' => ['summoner', SummonerProjection::class],
    ];

    private readonly DatasetRef $ref;

    public function __construct(
        private readonly LegacyCatalog $catalog,
        string $version,
        string $lang,
    ) {
        $this->ref = new DatasetRef($version, $lang);
    }

    /** @return array<string, mixed> */
    public function build(): array
    {
        $document = ['version' => $this->ref->version, 'language' => $this->ref->lang];
        foreach (self::RESOURCES as $key => [$type, $class]) {
            $entries = (new $class($this->catalog, $this->ref))->entries();
            $document[$key] = [
                'contentLanguage' => $this->catalog->contentLanguage(
                    $type,
                    $this->ref->version,
                    $this->ref->lang,
                ),
                'entries'         => $entries,
            ];
        }

        return $document;
    }
}
