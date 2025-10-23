<?php
// src/Service/SummonerManager.php
namespace App\Service\API;

use App\Service\API\AbstractManager;
use App\Service\API\CategoriesInterface;

final class SummonerManager extends AbstractManager implements CategoriesInterface
{
    private const TYPE = 'summoner';

    /**
     * Récupère la liste complète des sorts d’invocateur depuis Riot (DDragon)
     * pour une version et une langue données, puis l’enregistre dans le dossier public/upload.
     *
     * - Vérifie d’abord si le fichier JSON existe déjà en local (upload/{version}/{lang}/summoner/summoner.json).
     * - Si oui, lit et retourne son contenu.
     * - Sinon, télécharge le JSON depuis DDragon, puis l’enregistre via l’UploadManager.
     *
     * @param string $version Version du jeu ciblée (ex. "15.1.1").
     * @param string $lang    Langue/locale des données (ex. "fr_FR").
     *
     * @return array Tableau associatif représentant la totalité des sorts d’invocateur (clés "data", "type", "version", etc.).
     *
     * @throws \RuntimeException Si le fichier local est illisible ou si le téléchargement échoue.
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
     * Recherche un sort d’invocateur par son identifiant unique dans les données DDragon.
     *
     * - Utilise getData() pour récupérer la liste complète des sorts d’invocateur.
     * - Parcourt le tableau et retourne la définition correspondant à l’ID fourni.
     * - Si aucun sort ne correspond, une RuntimeException est levée.
     *
     * @param string $name    Identifiant du sort recherché (ex. "SummonerFlash").
     * @param string $version Version du jeu (ex. "15.1.1").
     * @param string $lang    Langue/locale des données (ex. "fr_FR").
     *
     * @return array Tableau associatif décrivant le sort d’invocateur (nom, description, cooldown, etc.).
     *
     * @throws \RuntimeException Si aucun sort d’invocateur avec cet ID n’est trouvé.
     */
    public function getByName(string $name, string $version, string $lang): array{
        
        (array) $data = $this->getData($version,$lang)['data'];

        // Recherche de l'invocateur par id
        foreach ($data as $d) {
            if (isset($d['id']) && $d['id'] === $name) {
                return $d;
            }
        }
        // Si non trouvé
        throw new \RuntimeException(sprintf('Aucun invocateur trouvé avec l\'ID "%s".', $name));
    }

    /**
     * Recherche les sorts d'invocateur dont l'ID ou le nom contient une chaîne donnée.
     *
     * La recherche est insensible à la casse et peut retourner plusieurs résultats.
     *
     * @param string $name    Sous-chaîne à rechercher (ex: "riere").
     * @param string $version Version du jeu (ex: "15.12.1").
     * @param string $lang    Code de langue (ex: "fr_FR").
     *
     * @return array[] Liste des sorts d'invocateur correspondants.
     *
     * @throws \RuntimeException Si le format des données est invalide.
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

        foreach ($data as $summoner) {
            if($max !== 0 && count($results) >= $max){
                break;
            }
            $idMatch   = isset($summoner['id']) && str_contains(mb_strtolower($summoner['id']), $search);
            $nameMatch = isset($summoner['name']) && str_contains(mb_strtolower($summoner['name']), $search);

            if (($idMatch || $nameMatch)) {
                $results[] = $summoner;
            }
        }

        return $results;
    }

    /**
     * Récupère les images des sorts d'invocateur (Summoner Spells).
     *
     * Si aucun tableau de données n'est fourni, la méthode charge l'ensemble
     * des invocations disponibles via {@see getData()}.
     *
     * @param string $version Version du jeu à utiliser (ex. "15.1.1").
     * @param string $lang    Langue à utiliser (ex. "fr_FR").
     * @param bool   $force   Si true, force le téléchargement même si l'image existe déjà en cache.
     * @param array  $data    Données des sorts d'invocateur à traiter. 
     *                        Si vide, toutes les données disponibles seront utilisées.
     *
     * @return array Tableau associatif : clé = ID du sort (ex. "SummonerFlash"),
     *               valeur = chemin local de l'image correspondante.
     */
    public function getImages(string $version, string $lang, bool $force = false, array $data = []): array
    {
        if(!$data){
            (array) $data = array_values($this->getData($version, $lang)['data'] ?? []);
        }

        $dir = $this->buildDir($version, $lang, SELF::TYPE, true);

        $result = [];

        foreach ($data as $d) {
            $id  = $d['id'] ?? null;
            $img = $d['image']['full'] ?? null;

            if (!$id || !$img) {
                continue;
            }

            $result[$id] = $this->getImage($img, $version, $dir, $force);
        }

        return $result;
    }
    
    /**
     * Télécharge et enregistre l’icône d’un sort d’invocateur depuis Riot (DDragon)
     * dans le dossier public du projet : upload/{version}/summoner_img/{name}.
     *
     * - Construit le chemin de stockage si $dir n’est pas fourni.
     * - Si l’image existe déjà et que $force = false, retourne immédiatement le chemin relatif.
     * - Sinon, télécharge l’image depuis DDragon, compare le binaire à ceux déjà stockés
     *   (via binaryExisting), puis crée un lien symbolique ou enregistre le fichier.
     *
     * @param string $name    Nom du fichier de l’icône (ex. "SummonerFlash.png").
     * @param string $version Version du jeu ciblée (ex. "15.1.1").
     * @param array  $dir     Répertoire cible pré-construit (optionnel, ex. retour de buildDirAndPath()).
     * @param bool   $force   Si true, force le téléchargement même si l’image existe déjà.
     * @param string $lang    Locale (ignorée ici, présente uniquement pour compatibilité de signature).
     *
     * @return string Chemin relatif du fichier enregistré (ex. "upload/15.1.1/summoner_img/SummonerFlash.png").
     *
     * @throws \RuntimeException En cas d’échec de téléchargement ou d’écriture du fichier.
     */
    public function getImage(string $name, string $version, array $dir = [], bool $force = false, string $lang = ''):string {
        $baseUrl = "https://ddragon.leagueoflegends.com/cdn/{$version}/img/spell/";

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
     * Paginateur pour la liste des sorts d’invocateur (Summoner Spells).
     *
     * Cette méthode récupère la liste complète des sorts pour une version et une langue données,
     * calcule le nombre total de pages en fonction du nombre d’éléments par page demandé,
     * et retourne uniquement la tranche correspondant à la page courante avec leurs images associées.
     *
     * @param string $version  Version du jeu à utiliser (ex. "15.1.1").
     * @param string $langue   Code de langue à utiliser (ex. "fr_FR").
     * @param int    $nb       Nombre d’éléments par page. Si 0 ou supérieur au total, tout est affiché.
     * @param int    $numPage  Numéro de la page à afficher (1 par défaut).
     *
     * @return array{
     *     summoners: array,   // Liste paginée des sorts d’invocateur
     *     images: array,      // Tableau des chemins d'images associés
     *     meta: array{        // Informations de pagination
     *         currentPage: int,
     *         nombrePage: int,
     *         itemPerPage: int,
     *         totalItem: int,
     *         type: string
     *     },
     * }
     */
    public function paginate(string $version, string $langue, int $nb = 1, int $numPage = 1): array{

        (array) $json = $this->getData($version, $langue)['data'];

        (int) $ttSum = count($json);
        if($nb === 0 || $nb > $ttSum){
            $nb = $ttSum;
        }
        $ttPage = ceil($ttSum / $nb);
        if( $numPage > $ttPage ){
            $numPage = 1;
        }
        
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
