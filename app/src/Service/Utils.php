<?php
declare(strict_types=1);

namespace App\Service;

use Symfony\Component\Filesystem\Path;
use Symfony\Component\Filesystem\Filesystem;

final class Utils
{
    public function __construct(
        private Filesystem $fs,
        private readonly VersionManager $versionManager,
        private readonly string $baseDir,
    ) {}

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

    /**
     * A Spliter dans un service d'edition TXT
     * Normalise un tag BCP47 en format "xx_YY"
     * - 'fr'      -> 'fr'
     * - 'fr-FR'   -> 'fr_FR'
     * - 'EN-us'   -> 'en_US'
     */
    public function normalizeTag(string $tag): string
    {
        $tag = str_replace('-', '_', $tag);

        if (str_contains($tag, '_')) {
            [$lang, $region] = explode('_', $tag, 2);
            return strtolower($lang) . '_' . strtoupper($region);
        }

        return strtolower($tag);
    }

    /**
     * Construit le dossier cible (relatif + absolu) pour un type de ressource.
     * Règles :
     *  - JSON (img=false) : upload/{version}/{lang}/{type}
     *  - IMG  (img=true)  : upload/{version}/{type}_img   (lang ignoré)
     *
     * Aucun accès disque ici : fonction pure.
     *
     * @param string $version Ex.: "15.1.1"
     * @param string $lang    Ex.: "fr_FR" (ignoré si $img === true)
     * @param string $type    Ex.: "summoner", "champion", "rune", ...
     * @param bool   $img     true => chemin dédié aux images
     *
     * @return array{relDir:string, absDir:string} Dossiers relatif et absolu.
     */
    public function buildDir(string $version, string $lang, string $type, bool $img = false): array{
        $relDir = $img
            ? "upload/{$version}/{$type}_img"
            : "upload/{$version}/{$lang}/{$type}";
        $absDir  = Path::join($this->baseDir, $relDir); //Chemin absolut
        $this->fs->mkdir($absDir);
        return [
            'relDir' => $relDir,
            'absDir' => $absDir,
        ];
    }

    /**
     * Construit les chemins (relatif/absolu) d’un fichier à partir d’un dossier construit par buildDir().
     *
     * @param array{relDir:string, absDir:string} $dir Dossier retourné par buildDir().
     * @param string $name                           Nom de fichier (ex.: "summoner.json", "Flash.png").
     *
     * @return array{relPath:string, absPath:string, nameFile:string} Chemins + nom.
     */
    public function buildPath(array $dir, string $name): array{
        $relPath = Path::join($dir['relDir'], $name);
        $absPath = Path::join($dir['absDir'], $name);
        $this->fs->mkdir($dir['absDir']);
        return [
            'relPath' => $relPath,
            'absPath' => $absPath,
            'fileName' => $name,
        ];
    }

    /**
     * Helper combinant buildDir() + buildPath() en un seul appel.
     *
     * @param string $version Ex.: "15.1.1"
     * @param string $lang    Ex.: "fr_FR"
     * @param string $type    Ex.: "summoner"
     * @param string $name    Ex.: "summoner.json" ou "Flash.png"
     * @param bool   $img     true => chemin image (lang ignoré)
     *
     * @return array{
     *   relDir:string,
     *   absDir:string,
     *   relPath:string,
     *   absPath:string,
     *   nameFile:string
     * }
     */
    public function buildDirAndPath(string $version, string $lang, string $type, string $name, bool $img = false): array{
        $dir = $this->buildDir($version, $lang, $type, $img);
        $path = $this->buildPath($dir, $name);
        return array_merge($dir, $path);
    }

    

    /**
     * Recherche un doublon binaire de l’image dans les autres versions et renvoie son chemin.
     *
     * Parcourt toutes les versions retournées par VersionManager et, pour chacune,
     * construit le chemin `upload/{version}/{type}_img/{name}`. Si un fichier existe,
     * il compare d’abord la longueur puis le contenu **octet à octet** avec `$bin`.
     * Le premier fichier strictement identique trouvé est retourné.
     *
     * @param string $bin  Contenu binaire du fichier courant (ex. bytes de l’image).
     * @param string $name Nom de fichier (avec extension), ex. "SummonerBarrier.png".
     * @param string $type Type logique utilisé dans le chemin (ex. "summoner" → dossier "{type}_img").
     *
     * @return string|null Chemin **relatif** vers le doublon (ex. "upload/15.4.1/summoner_img/SummonerBarrier.png"),
     *                     ou `null` si aucun fichier identique n’a été trouvé.
     *
     * @note Compare le contenu en mémoire (`===`) ; retourne le **premier** match rencontré.
     */
    public function binaryExisting(string $bin, string $name, string $type): ?string{
        if(!$bin){
            return null;
        }
        $versions = $this->versionManager->getVersions();
        foreach($versions as $version){
            $path = "upload/{$version}/{$type}_img/$name";
            $file = $this->fileIsExisting($path);
            if(!$file){
                continue;
            }
            if(strlen($bin) !== strlen($file)){
                continue;
            }
            if($bin === $file){
                return $path;
            }
        }
        return null;
    }

}
