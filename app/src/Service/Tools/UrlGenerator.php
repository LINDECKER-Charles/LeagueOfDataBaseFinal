<?php

namespace App\Service\Tools;

use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;

class UrlGenerator {

    public function __construct(
        private readonly RequestStack $requestStack,
        private readonly UrlGeneratorInterface $router,
    ) {}

    
    /**
     * Génère une URL de retour (backurl) en se basant sur l'en-tête HTTP "referer".
     *
     * Comportement :
     * 1. Si la requête courante est absente → retourne la route de fallback.
     * 2. Si aucun "referer" n'est présent dans les en-têtes → retourne la route de fallback.
     * 3. Si l'option $sameHostOnly est activée, vérifie que le referer partage le même host :
     *    - si ce n'est pas le cas → retourne la route de fallback.
     * 4. Si le referer pointe vers la racine `/` → redirige automatiquement vers la route `app_home`.
     * 5. Dans tous les autres cas → retourne l'URL du referer tel quel.
     *
     * @param string $fallbackRoute   Nom de la route Symfony à utiliser comme URL de secours
     *                                si aucune URL valide ne peut être générée (par défaut "app_setup").
     * @param array  $fallbackParams  Paramètres éventuels à passer à la route de fallback.
     * @param bool   $sameHostOnly    Si `true`, n'autorise que des referers provenant du même host
     *                                que la requête courante (sécurité contre redirection externe).
     *
     * @return string L'URL de retour (soit le referer, soit `/home` si referer == `/`,
     *                soit l'URL générée de fallback).
     *
     * @see \Symfony\Component\HttpFoundation\Request::headers
     * @see \Symfony\Component\Routing\RouterInterface::generate()
     */
    public function generateBackurl(
        string $fallbackRoute = 'app_setup',
        array $fallbackParams = [],
        bool $sameHostOnly = true
    ) {
        // On récupère la requête
        $request = $this->requestStack->getCurrentRequest();

        // On génère la route de fallback en cas d'erreur
        $fallback = $this->router->generate($fallbackRoute, $fallbackParams);
        if (!$request) {
            return $fallback;
        }

        $referer = (string) ($request->headers->get('referer') ?? '');

        if ($referer === '') {
            return $fallback;
        }

        if ($sameHostOnly) {
            $host = $request->getSchemeAndHttpHost();
            if ($host === '' || !str_starts_with($referer, $host)) {
                return $fallback;
            }
        }

        // Vérifie si l'URL précédente correspond à la racine `/`
        $path = parse_url($referer, PHP_URL_PATH);
        if ($path === '/' || $path === '') {
            return $this->router->generate('app_home');
        }

        return $referer;
    }


    /**
     * 2) Réécrit l'URL en remplaçant/supprimant des query params,
     *    sauf si le path fait partie d’une liste de "skip".
     *
     * @param string $url          URL initiale
     * @param array  $overrides    ['version' => '15.1.1', 'lang' => 'fr_FR'] (valeur null => suppression)
     * @param array  $removeKeys   ['foo','bar'] : clés à supprimer avant overrides
     * @param array  $skipPaths    ['/','/working-progress'] : ne rien toucher si path ∈ liste
     */
    public function rewriteQueryParams(
        string $url,
        array $overrides = [],
        array $removeKeys = [],
        array $skipPaths = ['/', '/working-progress']
    ): string {
        // Décompose (compat: query & fragment)
        $path   = parse_url($url, PHP_URL_PATH)   ?? '/';
        $query  = parse_url($url, PHP_URL_QUERY)  ?? '';
        $frag   = parse_url($url, PHP_URL_FRAGMENT);

        // Si on est sur un path à ignorer, on ressort tel quel
        if (in_array($path, $skipPaths, true)) {
            return $url;
        }

        // Query existante -> tableau
        $queryParams = [];
        if ($query !== '') {
            parse_str($query, $queryParams);
        }

        // Supprime d'abord les clés demandées
        foreach ($removeKeys as $k) {
            unset($queryParams[$k]);
        }

        // Applique les overrides (null => suppression)
        foreach ($overrides as $k => $v) {
            if ($v === null) {
                unset($queryParams[$k]);
            } else {
                $queryParams[$k] = $v;
            }
        }

        // Reconstruit query & URL finale (préserve #fragment)
        $newQuery = http_build_query($queryParams);
        $newUrl   = $path . ($newQuery !== '' ? '?' . $newQuery : '');
        if ($frag !== null && $frag !== '') {
            $newUrl .= '#' . $frag;
        }

        return $newUrl;
    }
}