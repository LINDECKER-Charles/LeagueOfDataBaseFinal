<?php
// src/Service/RiotManager.php
namespace App\Service;

use App\Service\Utils;
use App\Service\APICaller;
use App\Service\VersionManager;
use Symfony\Component\Filesystem\Path;
use function PHPUnit\Framework\fileExists;
use Symfony\Component\Filesystem\Filesystem;
use Symfony\Contracts\HttpClient\HttpClientInterface;

final class RiotManager{
    public function __construct(
        private readonly HttpClientInterface $http,
        private readonly UploadManager $uploader,
        private readonly APICaller $aPICaller,
        private readonly Utils $utils,
        private readonly string $baseDir,
        private readonly Filesystem $fs = new Filesystem(),
        private readonly VersionManager $versionManager,
    ) {}

    /**
     * Récupère un fichier JSON depuis le cache local ou l'API DDragon de Riot Games.
     *
     * - Vérifie si le fichier existe déjà en local, et le retourne si c'est le cas.
     * - Sinon, télécharge le fichier depuis l'API Riot.
     * - Sauvegarde le JSON téléchargé sur le disque.
     * - Retourne le contenu du fichier JSON en chaîne.
     *
     * @param string $version   Version du jeu (ex. "15.1.1").
     * @param string $lang      Langue/locale des données (ex. "fr_FR").
     * @param string $type      Type de données Riot (ex. "summoner", "object", "champion").
     * @param string $filename  Nom du fichier attendu (ex. "summoner.json", "item.json").
     *
     * @return string Contenu JSON brut (sous forme de chaîne).
     *
     * @throws \RuntimeException Si la récupération ou la sauvegarde échoue.
     */
    public function getJson(string $version, string $lang, string $type, string $filename = null): array
    {
        if(!$filename){
            $filename = "{$type}.json";
        }
        $path = $this->utils->buildDirAndPath($version, $lang, $type, $filename);

        // Cache local
        if ($file = $this->utils->fileIsExisting($path['absPath'])) {
            return json_decode($file, true);
        }

        // Fetch DDragon
        $data = json_decode($this->aPICaller->call("https://ddragon.leagueoflegends.com/cdn/{$version}/data/{$lang}/{$filename}"), true);

        // Sauvegarde
        $this->uploader->saveJson($path['absDir'], $path['fileName'], $data, false);

        return $data;
    }

    /**
     * Recherche une entrée spécifique dans les données Riot (DDragon) en fonction d'une clé.
     *
     * - Récupère le JSON via RiotManager::getJson().
     * - Décode les données et vérifie la présence de la clé "data".
     * - Parcourt les entrées et renvoie celle dont la valeur de $key correspond à $name.
     *
     * @param string $name    Valeur recherchée (ex. "SummonerFlash" si $key = "id").
     * @param string $version Version du jeu (ex. "15.1.1").
     * @param string $lang    Langue/locale des données (ex. "fr_FR").
     * @param string $key     Nom de la clé utilisée pour la comparaison (par défaut "id").
     * @param string $type    Type de données Riot (par défaut "summoner", ex. "object", "champion").
     *
     * @return array Tableau associatif représentant l'entrée trouvée.
     *
     * @throws \RuntimeException Si le format des données est invalide ou si aucune entrée correspondante n'est trouvée.
     */
    public function getDataByKey(string $name, string $version, string $lang, string $key = 'id', string $type = 'summoner', bool $noData = false): array{

        (array) $data = $this->getJson($version,$lang, $type);

        if($key === ''){
            return $data['data'][$name];
        }else if(!$noData){
            $data = $data['data'];
        }

        // Recherche de l'invocateur par id
        foreach ($data as $d) {
            if (isset($d[$key]) && $d[$key] === $name) {
                return $d;
            }
        }

        // Si non trouvé
        throw new \RuntimeException(sprintf('Aucun invocateur trouvé avec l\'ID "%s".', $name));
    }

    /**
     * Récupère une image depuis l’API Data Dragon (ou le cache local).
     *
     * - Vérifie si l’image existe déjà en local, sinon la télécharge.
     * - Utilise un lien symbolique si une copie identique est déjà présente.
     *
     * @param string      $name     Nom du fichier image (ex: "1001.png", "SummonerFlash.png").
     * @param string      $version  Version du jeu (ex: "15.1.1").
     * @param array       $dir      Dossier de destination (chemins relatifs et absolus) ; généré si vide.
     * @param bool        $force    Si true, force le téléchargement même si l’image existe déjà en local.
     * @param string      $lang     Langue utilisée pour la construction du chemin (facultatif).
     * @param string|null $type     Type de ressource (ex: "item", "summoner", "champion"...).
     * @param string|null $baseUrl  URL de base personnalisée pour le téléchargement (sinon auto-déduite selon $type).
     *
     * @return string  Chemin relatif de l’image dans le projet (utilisable côté front).
     */
    public function getImage(string $name, string $version, array $dir = [], bool $force = false, string $lang = '', string $type = null, string $baseUrl = null):string {
        if(!$baseUrl){
            if($type === 'summoner'){
                $baseUrl = "https://ddragon.leagueoflegends.com/cdn/{$version}/img/spell/";
            }else{
                $baseUrl = "https://ddragon.leagueoflegends.com/cdn/{$version}/img/{$type}/";
            }
        }
        if(!$dir){
            $dir = $this->utils->buildDirAndPath($version, $lang, $type, $name, true);
        }
        $path = $this->utils->buildPath($dir, $name);
        if (!$force && $this->fs->exists($path['absPath'])) {
            return $path['relPath'];
        }

        $binary = $this->aPICaller->call($baseUrl . $name);
        if($src = $this->utils->binaryExisting($binary, $name, $type)){
            @link($src, $path['absPath']);
        }else{
            $this->fs->dumpFile($path['absPath'], $binary);
        }

        return $path['relPath'];
    }

    /**
     * Récupère et met en cache un lot d’images pour un type d’objet Data Dragon.
     *
     * - Parcourt les données fournies (ou récupère le JSON complet si absent).
     * - Télécharge les images associées si nécessaire (via getImage).
     *
     * @param string $version  Version du jeu (ex: "15.1.1").
     * @param string $lang     Langue utilisée (ex: "fr_FR").
     * @param bool   $force    Si true, force le téléchargement de toutes les images même si elles existent déjà.
     * @param string $type     Type de données (ex: "item", "champion", "summoner"...).
     * @param string $key      Nom de la clé identifiant l’objet (ex: "id" ou "key").
     * @param array  $data     Données JSON préchargées (facultatif). Si vide, appelle getJson().
     *
     * @return array  Tableau associatif [id => cheminRelatifImage].
     */
    public function getImages(string $version, string $lang, bool $force = false, 
    string $type, string $key, 
    array $data = []): array{
        if(!$data){
            (array) $data = array_values($this->getJson($version, $lang, $type)['data'] ?? []);
        }

        $dir = $this->utils->buildDir($version, $lang, $type, true);

        $result = [];

        foreach ($data as $d) {
            $id  = $d[$key] ?? null;
            $img = $d['image']['full'] ?? null;

            if (!$id || !$img) {
                continue;
            }

            $result[$id] = $this->getImage($img, $version, $dir, $force, $lang, $type);
        }

        return $result;
    }

    
    /**
     * Paginateur générique pour les données Data Dragon (items, summoners, champions...).
     *
     * - Découpe la liste complète des objets en pages.
     * - Retourne la page demandée + les images correspondantes.
     *
     * @param string $version  Version du jeu (ex: "15.1.1").
     * @param string $langue   Langue utilisée (ex: "fr_FR").
     * @param string $type     Type de données (ex: "item", "summoner", "champion"...).
     * @param string $key      Nom de la clé identifiant l’objet (ex: "id" ou "key").
     * @param int    $nb       Nombre d’objets par page (0 = tout).
     * @param int    $numPage  Numéro de la page à récupérer (1 par défaut).
     *
     * @return array {
     *     @var array $items   Données JSON des objets de la page courante.
     *     @var array $images  Tableau associatif [id => cheminRelatifImage].
     *     @var array $meta    Métadonnées de pagination :
     *                         - currentPage : numéro de la page courante
     *                         - nombrePage  : nombre total de pages
     *                         - itemPerPage : nombre d’objets par page
     *                         - totalItem   : nombre total d’objets
     *                         - type        : type de données (ex: "item")
     * }
     */
    public function paginate(string $version, string $langue, string $type, string $key, int $nb = 1, int $numPage = 1): array{

        (array) $json = $this->getJson($version, $langue, $type)['data'];

        (int) $ttSum = count($json);
        if($nb === 0 || $nb > $ttSum){
            $nb = $ttSum;
        }
        $ttPage = ceil($ttSum / $nb);
        if( $numPage > $ttPage ){
            $numPage = 1;
        }
        
        if($numPage <= 1){
            (array) $json = $this->utils->splitJson($nb, 0, $json);
        }else{
            (array) $json = $this->utils->splitJson($nb, $nb*($numPage-1), $json);
        }
        
        (array) $images = $this->getImages($version, $langue, false, $type, $key, $json);
        return [
            $type . 's' =>  $json,
            'images' => $images,
            'meta' => [
                'currentPage' => $numPage,
                'nombrePage' => $ttPage,
                'itemPerPage' => $nb,
                'totalItem' => $ttSum,
                'type' => $type,
            ],
        ];
    }
}