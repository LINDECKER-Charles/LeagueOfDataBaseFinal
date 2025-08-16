<?php
// src/Service/ObjectManager.php
namespace App\Service;

use App\Service\Utils;
use App\Service\APICaller;
use App\Service\RiotManager;
use App\Service\VersionManager;
use Symfony\Component\Filesystem\Path;
use function PHPUnit\Framework\fileExists;
use Symfony\Component\Filesystem\Filesystem;
use Symfony\Contracts\HttpClient\HttpClientInterface;

final class ItemManager{
    public function __construct(
        private readonly HttpClientInterface $http,
        private readonly UploadManager $uploader,
        private readonly APICaller $aPICaller,
        private readonly Utils $utils,
        private readonly string $baseDir,
        private readonly Filesystem $fs = new Filesystem(),
        private readonly VersionManager $versionManager,
        private readonly RiotManager $riotManager,
    ) {}

    /**
     * Récupère la liste des objets depuis Riot (DDragon).
     *
     * - Utilise RiotManager pour chercher les données JSON correspondantes.
     * - Retourne le contenu JSON brut sous forme de chaîne.
     *
     * @param string $version Version du jeu (ex. "15.1.1").
     * @param string $lang    Langue/locale des données (ex. "fr_FR").
     *
     * @return string Contenu JSON des objets.
     */
    public function getObjects(string $version, string $lang): array
    {
        return $this->riotManager->getJson($version, $lang, 'item');
    }

    /**
     * Récupère les données d’un objet (item) spécifique depuis Data Dragon.
     *
     * @param string $name     Nom exact de l’objet (clé unique, ex: "1001").
     * @param string $version  Version du jeu (ex: "15.1.1").
     * @param string $lang     Langue à utiliser (ex: "fr_FR").
     *
     * @return array  Tableau associatif contenant toutes les infos de l’objet (nom, description, gold, tags, stats...).
     */
    public function getObjectByName(string $name, string $version, string $lang): array{
        return $this->riotManager->getDataByKey($name, $version, $lang, '', 'item');
    }

    /**
     * Récupère et met en cache les images associées à un ou plusieurs objets (items).
     *
     * @param string $version  Version du jeu (ex: "15.1.1").
     * @param string $lang     Langue à utiliser (ex: "fr_FR").
     * @param bool   $force    Si true, force le téléchargement même si l’image existe déjà en cache.
     * @param array  $items    Liste d’items (sous-tableau du JSON) pour ne traiter qu’un sous-ensemble (facultatif).
     *
     * @return array  Tableau associatif [nomItem => cheminRelatifImage].
     */
    public function getObjectsImages(string $version, string $lang, bool $force = false, array $items = []): array
    {
        return $this->riotManager->getImages($version, $lang, $force, 'item', 'name', $items);
    }
    
    /**
     * Récupère (ou télécharge si nécessaire) l’image d’un objet (item) spécifique.
     *
     * @param string $name     Nom du fichier image (ex: "1001.png").
     * @param string $version  Version du jeu (ex: "15.1.1").
     * @param array  $dir      Répertoire de destination (facultatif, généré automatiquement si vide).
     * @param bool   $force    Si true, force le téléchargement même si l’image existe déjà en local.
     * @param string $lang     Langue à utiliser (ex: "fr_FR", facultatif).
     *
     * @return string  Chemin relatif vers l’image (ex: "upload/15.1.1/fr_FR/item/1001.png").
     */
    public function getObjectImage(string $name, string $version, array $dir = [], bool $force = false, string $lang = ''):string {
        return $this->riotManager->getImage($name, $version, $dir, $force, $lang,  'item');
    }

    /**
     * Paginateur pour la liste complète des objets (items) de Data Dragon.
     *
     * - Découpe les items en pages selon $nb.
     * - Retourne les données et les images de la page courante + métadonnées.
     *
     * @param string $version  Version du jeu (ex: "15.1.1").
     * @param string $langue   Langue à utiliser (ex: "fr_FR").
     * @param int    $nb       Nombre d’objets par page (0 = tous les items).
     * @param int    $numPage  Numéro de la page à afficher (1 par défaut).
     *
     * @return array {
     *     @var array $items   Données JSON des objets de la page courante.
     *     @var array $images  Tableau associatif [nomItem => cheminRelatifImage].
     *     @var array $meta    Métadonnées de pagination :
     *                         - currentPage : numéro de la page courante
     *                         - nombrePage  : nombre total de pages
     *                         - itemPerPage : nombre d’objets par page
     *                         - totalItem   : nombre total d’objets
     *                         - type        : "item"
     * }
     */
    public function paginateItems(string $version, string $langue, int $nb = 1, int $numPage = 1): array{
        return $this->riotManager->paginate($version, $langue, 'item', 'name', $nb, $numPage);
    }

    public function searchObjectsByName(string $name, string $version, string $lang, int $max = 0): array
    {
        if (mb_strlen($name) < 2 || mb_strlen($name) > 50) {
            throw new \InvalidArgumentException('Nom invalide.');
        }

        (array) $data = $this->getObjects($version, $lang)['data'];

        if (!isset($data) || !is_array($data)) {
            throw new \RuntimeException('Format de données invalide.');
        }

        $results = [];
        $search = mb_strtolower($name); // normalisation pour recherche insensible à la casse

        foreach ($data as $key => $item) {
            if($max !== 0 && count($results) >= $max){
                break;
            }
            $nameMatch = isset($item['name']) && str_contains(mb_strtolower($item['name']), $search);

            if (($nameMatch)) {
                $results[] = array_merge($item, ['id' => $key]);
            }
        }

        return $results;
    }

}