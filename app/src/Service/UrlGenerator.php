<?php

namespace App\Service;

use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;

class UrlGenerator {

    public function __construct(
        private readonly RequestStack $requestStack,
        private readonly UrlGeneratorInterface $router,
    ) {}

    
    /**
     * 1) Back URL sûr : referer si même host, sinon fallback route.
     */
    public function generateBackurl(
        string $fallbackRoute = 'app_setup',
        array $fallbackParams = [],
        bool $sameHostOnly = true
    ){

        //On récupère la requête
        $request = $this->requestStack->getCurrentRequest();

        //On génère la route de retour en cas d'erreur
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