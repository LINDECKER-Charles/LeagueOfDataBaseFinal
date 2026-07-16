<?php
declare(strict_types=1);

namespace App\Service\Tools;

/**
 * Levée par {@see GoFetcherClient::fetch()} quand Data Dragon répond un statut
 * d'absence *définitif* (403/404) pour une ressource JSON — par opposition à une
 * panne transitoire (5xx, timeout, erreur transport).
 *
 * Les appelants s'en servent pour dégrader proprement (jeu de données vide,
 * repli de langue) au lieu de propager une erreur, tout en laissant les vraies
 * pannes remonter : une ressource légitimement absente pour une (version, langue)
 * — p.ex. runesReforged avant le patch 7.22, ou une locale absente d'un vieux
 * patch — ne doit jamais casser une page ni être confondue avec un incident.
 */
final class UpstreamNotFoundException extends \RuntimeException
{
}
