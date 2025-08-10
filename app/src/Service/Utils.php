<?php
declare(strict_types=1);

namespace App\Service;

use Symfony\Component\Filesystem\Filesystem;

final class Utils
{
    public function __construct(private Filesystem $fs) {}

    /**
     * Retourne le contenu du fichier s'il existe, sinon null.
     *
     * @param string $absPath Chemin absolu du fichier.
     * @return string|null
     *
     * @throws \RuntimeException Si le fichier existe mais n'est pas lisible.
     */
    public function fileIsExisting(string $absPath): ?string
    {
        if ($this->fs->exists($absPath)) {
            $cached = @file_get_contents($absPath);
            if ($cached === false) {
                throw new \RuntimeException("Impossible de lire le fichier: $absPath");
            }
            return $cached;
        }
        return null;
    }

        /**
     * Décode une chaîne JSON et renvoie la structure PHP.
     *
     * @param string $json  Chaîne JSON à décoder.
     * @param bool   $assoc Si true, renvoie des tableaux associatifs (sinon des objets).
     * @param int    $depth Profondeur maximale de décodage.
     *
     * @return mixed Données décodées.
     *
     * @throws \RuntimeException Si le JSON est invalide.
     */
    public function decodeJson(string $json, bool $assoc = true, int $depth = 512): mixed
    {
        $data = json_decode($json, $assoc, $depth);
        if (json_last_error() !== JSON_ERROR_NONE) {
            throw new \RuntimeException('JSON invalide: ' . json_last_error_msg());
        }
        return $data;
    }

    /**
     * Encode des données PHP en JSON.
     *
     * @param mixed $data  Données à encoder.
     * @param int   $flags Options d'encodage JSON (ex: JSON_UNESCAPED_UNICODE).
     *
     * @return string Chaîne JSON encodée.
     *
     * @throws \RuntimeException Si l'encodage échoue.
     */
    public function encodeJson(mixed $data, int $flags = 0): string
    {
        $json = json_encode($data, $flags);
        if ($json === false) {
            throw new \RuntimeException('Échec encodage JSON: ' . json_last_error_msg());
        }
        return $json;
    }
}
