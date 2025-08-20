<?php
// src/Service/AbstractManager.php
namespace App\Service;

use App\Service\APICaller;
use App\Service\UploadManager;
use App\Service\VersionManager;
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

    /* Prototype */

    /**
     * Récupère les données pour une version et une langue données.
     *
     * @param string $version Version du jeu (ex. "15.1.1").
     * @param string $lang    Langue/locale des données (ex. "fr_FR").
     *
     * @return array Tableau associatif contenant les données récupérées.
     */
    abstract public function getData(string $version, string $lang): array;
    /**
     * Recherche une entrée par son identifiant dans les données de la version et langue spécifiées.
     *
     * @param string $name    Identifiant recherché (ex. "SummonerBarrier").
     * @param string $version Version cible (ex. "15.1.1").
     * @param string $lang    Langue/locale des données (ex. "fr_FR").
     *
     * @return array Tableau associatif représentant l'entrée trouvée.
     *
     * @throws \RuntimeException Si aucun élément correspondant à l'identifiant n'est trouvé.
     */
    abstract public function getByName(string $name, string $version, string $lang): array;
    /**
     * Recherche des entrées dont le nom correspond (totalement ou partiellement) à celui fourni.
     *
     * @param string $name    Nom ou fragment de nom à rechercher.
     * @param string $version Version cible (ex. "15.1.1").
     * @param string $lang    Langue/locale des données (ex. "fr_FR").
     * @param int    $max     Nombre maximum de résultats à retourner (0 = illimité).
     *
     * @return array Liste des entrées trouvées, sous forme de tableaux associatifs.
     *
     * @throws \RuntimeException Si la récupération des données échoue.
     */
    abstract public function searchByName(string $name, string $version, string $lang, int $max = 0): array;
    /**
     * Récupère l'image associée à une ressource du Data Dragon (champion, sort, objet, etc.).
     *
     * Cette méthode est abstraite : chaque Manager concret (ex: SummonerManager, ItemManager)
     * doit implémenter sa propre logique pour construire le chemin et télécharger/charger l'image.
     *
     * @param string $name   Nom ou identifiant de la ressource (ex. "SummonerFlash", "InfinityEdge").
     * @param string $version Version du jeu à utiliser (ex. "15.1.1").
     * @param array  $dir     Chemin(s) de répertoires personnalisés pour stocker l'image localement.
     *                        Si vide, un chemin par défaut sera utilisé.
     * @param bool   $force   Si true, force le téléchargement même si l'image existe déjà en cache.
     * @param string $lang    Langue à utiliser (ex. "fr_FR"). Peut être ignorée si non pertinente pour l'image.
     *
     * @return string Chemin absolu ou relatif de l'image disponible localement.
     */
    abstract public function getImage(string $name, string $version, array $dir = [], bool $force = false, string $lang = ''):string;
    /**
     * Récupère l'ensemble des images liées à une ressource (ex. les invocations).
     *
     * Chaque Manager concret doit implémenter sa propre logique pour déterminer
     * quelles ressources récupérer (champions, objets, sorts, etc.).
     *
     * @param string $version Version du jeu à utiliser (ex. "15.1.1").
     * @param string $lang    Langue à utiliser (ex. "fr_FR").
     * @param bool   $force   Si true, force le téléchargement même si les images existent déjà en cache.
     * @param array  $data    Liste optionnelle des ressources ciblées (ex. liste d'IDs). 
     *                        Si vide, toutes les ressources disponibles seront traitées.
     *
     * @return array Tableau associatif des ressources et de leur chemin local d'image.
     *               Clé = identifiant (ex. "SummonerFlash"), 
     *               Valeur = chemin vers l'image correspondante.
     */
    abstract public function getImages(string $version, string $lang, bool $force = false, array $data = []): array;
    /**
     * Paginateur générique pour une ressource (ex. Summoner Spells, Items, Champions).
     *
     * Cette méthode doit être implémentée par chaque Manager concret. 
     * Elle permet de calculer la pagination des données pour une version et une langue données,
     * et de retourner les éléments de la page courante avec leurs images associées.
     *
     * @param string $version  Version du jeu à utiliser (ex. "15.1.1").
     * @param string $langue   Code de langue à utiliser (ex. "fr_FR").
     * @param int    $nb       Nombre d’éléments par page. Si 0 ou supérieur au total, tout est affiché.
     * @param int    $numPage  Numéro de la page à afficher (1 par défaut).
     *
     * @return array{
     *     <TYPE>s: string,    // Nom du type de ressource (défini par SELF::TYPE)
     *     images: array,       // Tableau des chemins d'images associés
     *     meta: array{         // Informations de pagination
     *         currentPage: int,
     *         nombrePage: int,
     *         itemPerPage: int,
     *         totalItem: int,
     *         type: string
     *     },
     * }
     */
    abstract public function paginate(string $version, string $langue, int $nb = 1, int $numPage = 1): array;




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
    protected final function buildDirAndPath(string $version, string $lang, string $type, string $name = null, bool $img = false): array{
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