<?php

namespace App\Service\Tools;

use Symfony\Component\Filesystem\Filesystem;
use Symfony\Component\HttpFoundation\File\UploadedFile;
use Symfony\Component\HttpFoundation\File\Exception\FileException;

final class UploadManager
{
    public function __construct(
        private readonly Filesystem $fs,
        private readonly Utils $utils,
    ) {}

    /**
     * Enregistre un contenu au format JSON dans un fichier.
     *
     * Hypothèses :
     * - $dir et $filename sont fournis par l'application (pas de nettoyage effectué ici).
     * - $filename peut ou non contenir l'extension .json (aucune contrainte imposée ici).
     *
     * @param string $dir      Dossier cible (relatif ou absolu). Créé s'il n'existe pas.
     * @param string $filename Nom du fichier (ex. "data.json").
     * @param mixed  $content  Données à encoder en JSON (tableau, objet, scalaire…).
     *
     * @return string Chemin complet du fichier écrit.
     *
     * @throws \RuntimeException Si l'encodage JSON échoue.
     */
    public function saveJson(string $dir, string $filename, mixed $json, bool $encoded): string
    {
        // Crée le dossier si besoin
        $this->fs->mkdir($dir);

        $path = $dir . '/' . $filename;

        if(!$encoded){
            $json = json_encode($json);
        }
        if ($json === false) {
            throw new \RuntimeException(json_last_error_msg());
        }

        $this->fs->dumpFile($path, $json);

        return $path;
    }

        /**
     * Enregistre une image envoyée (UploadedFile) à l'emplacement donné.
     *
     * Hypothèses :
     * - $dir et $filename sont fournis par l'app (pas de nettoyage ici).
     * - Aucune validation de type MIME/extension n'est faite.
     *
     * @param string       $dir      Dossier cible (créé si besoin).
     * @param string       $filename Nom du fichier final (ex: "logo.png").
     * @param UploadedFile $file     Image uploadée depuis un formulaire.
     *
     * @return string Chemin complet du fichier écrit.
     *
     * @throws \RuntimeException Si le déplacement échoue.
     */
    public function saveImage(string $dir, string $filename, UploadedFile $file): string
    {
        // Crée le dossier si besoin
        $this->fs->mkdir($dir);

        try {
            // Déplace/renomme le fichier (écrase si existe selon le FS)
            $file->move($dir, $filename);
        } catch (FileException $e) {
            throw new \RuntimeException($e->getMessage(), 0, $e);
        }

        return rtrim($dir, '/').'/'.$filename;
    }
}
