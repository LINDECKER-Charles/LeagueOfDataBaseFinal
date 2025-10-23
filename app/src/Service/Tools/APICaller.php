<?php
declare(strict_types=1);

namespace App\Service\Tools;

use Symfony\Contracts\HttpClient\HttpClientInterface;

/**
 * Service ultra-simple d'appel HTTP.
 */
final class APICaller
{
    public function __construct(private readonly HttpClientInterface $http) {}

    /**
     * Effectue un GET sur l'URL donnée et renvoie le corps brut.
     *
     * @param string $url URL complète (http/https)
     * @return string Corps de la réponse (exception si statut non 2xx)
     *
     * @throws \RuntimeException En cas d'erreur réseau ou statut HTTP d'échec.
     */
    public function call(string $url): string
    {
        try {
            $response = $this->http->request('GET', $url);
            return $response->getContent();
        } catch (\Throwable $e) {
            throw new \RuntimeException('APICaller: échec requête GET: '.$e->getMessage(), 0, $e);
        }
    }
}