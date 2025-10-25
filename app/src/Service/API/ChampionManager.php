<?php
// src/Service/ObjectManager.php
namespace App\Service\API;

use App\Service\API\AbstractManager;

final class ChampionManager extends AbstractManager implements CategoriesInterface{

    private const TYPE = 'champion';


    /**
     * Récupère les données brutes des champions pour une version et une langue données.
     *
     * Tente d'abord de charger les données depuis le cache local si disponible.
     * Si le fichier n'existe pas, récupère les données depuis l'API Data Dragon de Riot,
     * puis les sauvegarde localement au format JSON.
     *
     * @param string $version Version du jeu (ex. "14.10.1").
     * @param string $lang    Code de langue (ex. "fr_FR", "en_US").
     *
     * @return array Données JSON décodées contenant tous les champions pour cette version et langue.
     *
     * @throws \RuntimeException Si l'appel à l’API échoue ou renvoie un format inattendu.
     */
    public function getData(string $version, string $lang): array
    {
        $path = $this->buildDirAndPath($version, $lang, SELF::TYPE);

        // Cache local
        if ($file = $this->fileIsExisting($path['absPath'])) {
            return json_decode($file, true);
        }

        // Fetch DDragon
        $data = json_decode($this->aPICaller->call("https://ddragon.leagueoflegends.com/cdn/{$version}/data/{$lang}/{$path['fileName']}"), true);

        // Sauvegarde
        $this->uploader->saveJson($path['absDir'], $path['fileName'], $data, false);

        return $data;
    }


    /**
     * Retourne les informations détaillées d’un champion spécifique à partir de son nom.
     *
     * Utilise les données chargées via getData() et recherche l’entrée correspondante
     * au nom du champion fourni (ex. "Aatrox"). Si aucun champion ne correspond,
     * une exception est levée.
     *
     * @param string $name    Nom ou identifiant interne du champion.
     * @param string $version Version du jeu.
     * @param string $lang    Code de langue.
     *
     * @return array Tableau associatif contenant les informations du champion.
     *
     * @throws \RuntimeException Si aucun champion n’est trouvé avec cet identifiant.
     */
    public function getByName(string $name, string $version, string $lang): array{
        
        (array) $data = $this->getData($version,$lang)['data'];

        if(isset($data[$name])){
            return $data[$name];
        }

        throw new \RuntimeException(sprintf('Aucun invocateur trouvé avec l\'ID "%s".', $name));
    }

    /**
     * Recherche des champions par nom ou identifiant partiel.
     *
     * Permet une recherche insensible à la casse dans les champs `id` et `name`
     * du fichier de données des champions. Limite les résultats à $max entrées si précisé.
     *
     * @param string $name    Terme recherché (au moins 2 caractères).
     * @param string $version Version du jeu.
     * @param string $lang    Code de langue.
     * @param int    $max     Nombre maximal de résultats à retourner (0 = illimité).
     *
     * @return array Tableau de champions correspondant à la recherche.
     *
     * @throws \InvalidArgumentException Si la longueur du nom est invalide.
     * @throws \RuntimeException         Si les données chargées sont corrompues ou mal formatées.
     */
    public function searchByName(string $name, string $version, string $lang, int $max = 0): array
    {
        if (mb_strlen($name) < 2 || mb_strlen($name) > 50) {
            throw new \InvalidArgumentException('Nom invalide.');
        }
        (array) $data = $this->getData($version, $lang)['data'];

        if (!isset($data) || !is_array($data)) {
            throw new \RuntimeException('Format de données invalide.');
        }

        $results = [];
        $search = mb_strtolower($name); // normalisation pour recherche insensible à la casse

        foreach ($data as $champion) {
            if($max !== 0 && count($results) >= $max){
                break;
            }
            $idMatch   = isset($champion['id']) && str_contains(mb_strtolower($champion['id']), $search);
            $nameMatch = isset($champion['name']) && str_contains(mb_strtolower($champion['name']), $search);

            if (($idMatch || $nameMatch)) {
                $results[] = $champion;
            }
        }

        return $results;
    }

    /**
     * Génère la liste des images locales associées aux champions.
     *
     * Télécharge et stocke les icônes des champions depuis le CDN Data Dragon.
     * Si le cache local est disponible, les chemins relatifs sont retournés sans rechargement.
     *
     * @param string $version Version du jeu.
     * @param string $lang    Code de langue.
     * @param bool   $force   Force le re-téléchargement même si le fichier existe déjà.
     * @param array  $data    Tableau de champions à traiter (facultatif).
     *
     * @return array Tableau des chemins relatifs vers les images locales des champions.
     */
    public function getImages(string $version, string $lang, bool $force = false, array $data = []): array
    {
        if(!$data){
            (array) $data = array_values($this->getData($version, $lang)['data'] ?? []);
        }
        
        $dir = $this->buildDir($version, $lang, SELF::TYPE, true);

        $result = [];
        foreach ($data as $d) {
            
            $id  = $d['name'] ?? null;
            $img = $d['image']['full'] ?? null;

            if (!$id || !$img) {
                continue;
            }

            $result[] = $this->getImage($img, $version, $dir, $force);
        }

        return $result;
    }
    
    /**
     * Télécharge ou retourne le chemin local d’une image de champion spécifique.
     *
     * Si l’image existe déjà localement et que $force est à false, le chemin local
     * est directement retourné. Sinon, l’image est téléchargée depuis Data Dragon
     * et enregistrée dans le répertoire correspondant à la version et la langue.
     *
     * @param string $name   Nom du fichier image (ex. "Aatrox.png").
     * @param string $version Version du jeu.
     * @param array  $dir    Tableau contenant les chemins de répertoire générés via buildDir().
     * @param bool   $force  Force le téléchargement même si le fichier existe déjà.
     * @param string $lang   Code de langue utilisé pour la génération du chemin.
     *
     * @return string Chemin relatif vers l’image locale sauvegardée.
     */
    public function getImage(string $name, string $version, array $dir = [], bool $force = false, string $lang = ''):string {
        
        $baseUrl = "https://ddragon.leagueoflegends.com/cdn/{$version}/img/". SELF::TYPE . "/";
        if(!$dir){
            $dir = $this->buildDir($version, $lang, SELF::TYPE, true);
        }
        $path = $this->buildPath($dir, $name);
        if (!$force && $this->fs->exists($path['absPath'])) {
            return $path['relPath'];
        }

        $binary = $this->aPICaller->call($baseUrl . $name);
        if($src = $this->binaryExisting($binary, $name, SELF::TYPE)){
            @link($src, $path['absPath']);
        }else{
            $this->fs->dumpFile($path['absPath'], $binary);
        }

        return $path['relPath'];
    }


    /**
     * Fournit une pagination complète sur la liste des champions disponibles.
     *
     * Charge toutes les données, calcule le nombre total de pages et retourne
     * uniquement les entrées correspondant à la page demandée. Inclut également
     * les métadonnées de pagination et les chemins d’images correspondants.
     *
     * @param string $version Version du jeu.
     * @param string $langue  Code de langue.
     * @param int    $nb      Nombre d’éléments par page (par défaut 20).
     * @param int    $numPage Numéro de la page demandée.
     *
     * @return array Tableau contenant :
     *               - 'champions' : liste partielle des champions,
     *               - 'images'    : chemins des images locales associées,
     *               - 'meta'      : métadonnées (page actuelle, total, etc.).
     */
    public function paginate(string $version, string $langue, int $nb = 1, int $numPage = 1): array{

        (array) $json = $this->getData($version, $langue)['data'];

        (int) $ttSum = count($json);
        if($nb === 0 || $nb > $ttSum){
            $ttSum > 20 ? $nb = 20 : $nb = $ttSum;
        }

        $ttPage = ceil($ttSum / $nb);
        if( $numPage > $ttPage ) $numPage = 1;

        
        
        if($numPage <= 1){
            (array) $json = $this->splitJson($nb, 0, $json);
        }else{
            (array) $json = $this->splitJson($nb, $nb*($numPage-1), $json);
        }
        
        (array) $images = $this->getImages($version, $langue, false, $json);
        
        return [
            SELF::TYPE . 's' =>  $json,
            'images' => $images,
            'meta' => [
                'currentPage' => $numPage,
                'nombrePage' => $ttPage,
                'itemPerPage' => $nb,
                'totalItem' => $ttSum,
                'type' => SELF::TYPE,
            ],
        ];
    }

}