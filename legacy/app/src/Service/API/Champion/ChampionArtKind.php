<?php
declare(strict_types=1);

namespace App\Service\API\Champion;

/**
 * The three unversioned champion art families hotlinked from the Data Dragon
 * CDN (`cdn/img/champion/<kind>/<id>_<skin>.jpg`). Values are the CDN path
 * segments — and the public vocabulary of the `champion_art()` Twig function.
 */
enum ChampionArtKind: string
{
    /** Wide 1215×717 splash art. */
    case Splash = 'splash';
    /** Portrait 308×560 loading-screen crop. */
    case Loading = 'loading';
    /** Centered 1280×720 crop used by the client's champion select. */
    case Centered = 'centered';
}
