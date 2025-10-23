<?php
// src/Service/AbstractManager.php
namespace App\Service\API;


use App\Service\Client\VersionManager;
use App\Service\Tools\APICaller;
use App\Service\Tools\UploadManager;
use Symfony\Component\Filesystem\Path;
use Symfony\Component\Filesystem\Filesystem;
use Symfony\Contracts\HttpClient\HttpClientInterface;

abstract class AbstractManager
{
    public function __construct(
        protected readonly string $baseDir,
        protected readonly HttpClientInterface $http,
        protected readonly UploadManager $uploader,
        protected readonly APICaller $aPICaller,
        protected readonly Filesystem $fs = new Filesystem(),
        protected readonly VersionManager $versionManager,
    ) {}

    /* Utils */
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
    protected final function buildDir(string $version, string $lang, string $type, bool $img = false): array{
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
    protected final function buildPath(array $dir, string $name): array{
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
     *   fileName:string
     * }
     */
    protected final function buildDirAndPath(string $version, string $lang, string $type, ?string $name = null, bool $img = false): array{
        if(!$name){
            $name = $type . '.json';
        }
        $dir = $this->buildDir($version, $lang, $type, $img);
        $path = $this->buildPath($dir, $name);
        return array_merge($dir, $path);
    }

    /**
     * Retourne le contenu du fichier s'il existe, sinon null.
     *
     * @param string $absPath Chemin absolu du fichier.
     * @return string|null
     *
     * @throws \RuntimeException Si le fichier existe mais n'est pas lisible.
     */
    protected final function fileIsExisting(string $absPath): ?string
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
     * Vérifie si un binaire existe déjà dans les versions locales.
     *
     * Compare la taille et le contenu du binaire donné avec les fichiers enregistrés.
     *
     * @param string $bin   Contenu binaire à vérifier.
     * @param string $name  Nom du fichier cible (ex. "Flash.png").
     * @param string $type  Type de ressource (ex. "summoner", "champion").
     *
     * @return string|null  Chemin relatif du fichier correspondant si trouvé, sinon null.
     */
    protected final function binaryExisting(string $bin, string $name, string $type): ?string{
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

    /**
     * Extrait une portion d’un tableau JSON.
     *
     * Utilise array_slice pour renvoyer une sous-partie du tableau d’entrée.
     *
     * @param int   $nb    Nombre d’éléments à extraire.
     * @param int   $start Index de départ (0-based).
     * @param array $json  Tableau source (JSON décodé).
     *
     * @return array       Portion du tableau correspondant.
     */
    protected final function splitJson(int $nb, int $start, array $json): array{
        return array_slice($json, $start, $nb, true);
    }
}