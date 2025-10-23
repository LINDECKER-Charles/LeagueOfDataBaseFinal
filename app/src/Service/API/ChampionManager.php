<?php
// src/Service/ObjectManager.php
namespace App\Service\API;

use App\Service\API\AbstractManager;

final class ChampionManager extends AbstractManager{

    private const TYPE = 'champion';

    /**
     * Récupère les données JSON des items via l’API Riot (Data Dragon)
     * et les stocke/relit depuis le répertoire public de l’application Symfony.
     *
     * - Vérifie si le fichier JSON des items correspondant à la version et à la langue
     *   existe déjà dans le répertoire public. Si oui, lit ce fichier et retourne son contenu décodé.
     * - Sinon, télécharge le JSON des items depuis l’API Riot, le sauvegarde dans le répertoire public,
     *   puis retourne les données décodées.
     *
     * @param string $version Version de League of Legends (ex. "15.1.1").
     * @param string $lang    Code de langue (ex. "fr_FR", "en_US").
     *
     * @return array Tableau associatif contenant les données décodées des items.
     *
     * @throws \RuntimeException En cas d’échec de lecture/écriture du fichier ou de JSON invalide.
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
     * Récupère les données d'un item précis par son identifiant, 
     * en fonction de la version et de la langue demandées.
     *
     * - Charge la liste complète des items via {@see getData()} depuis 
     *   le répertoire public (ou via l'API Riot si non présent).
     * - Vérifie si l'item demandé existe dans le tableau retourné.
     * - Retourne directement les données de l'item s'il est trouvé.
     * - Lance une exception sinon.
     *
     * @param string $name    Identifiant unique de l'item (ex. "1001" pour les Bottes).
     * @param string $version Version de League of Legends (ex. "15.1.1").
     * @param string $lang    Code de langue (ex. "fr_FR", "en_US").
     *
     * @return array Tableau associatif contenant les données de l'item trouvé.
     *
     * @throws \RuntimeException Si aucun item correspondant n'est trouvé.
     */ 
    public function getByName(string $name, string $version, string $lang): array{
        
        (array) $data = $this->getData($version,$lang)['data'];

        if(isset($data[$name])){
            return $data[$name];
        }

        throw new \RuntimeException(sprintf('Aucun invocateur trouvé avec l\'ID "%s".', $name));
    }

    /**
     * Télécharge (ou relit) les images des items pour une version et une langue données,
     * puis retourne un tableau associatif des résultats.
     *
     * Comportement :
     * - Si $data est vide, charge la liste des items via {@see getData()} et en extrait
     *   les entrées (champ 'data'), puis itère dessus.
     * - Pour chaque item, récupère le nom ($d['name']) et le nom de fichier image
     *   ($d['image']['full']), puis appelle {@see getImage()} pour persister/relire l’image
     *   dans le répertoire public correspondant à la version/langue/type.
     * - Construit et retourne un tableau: [ <nomItem> => résultatDeGetImage, ... ].
     *
     * @param string $version Version de League of Legends (ex. "15.1.1").
     * @param string $lang    Code de langue (ex. "fr_FR", "en_US").
     * @param bool   $force   Si true, (ré)télécharge l’image même si elle existe déjà en local.
     * @param array  $data    Liste d’items optionnelle. Si vide, utilise getData($version, $lang)['data'].
     *                        Chaque entrée doit contenir au minimum:
     *                        - 'name' (nom affiché de l’item)
     *                        - 'image'['full'] (nom de fichier image dans Data Dragon, ex. "1001.png")
     *
     * @return array Tableau associatif des résultats par nom d’item :
     *               [ 'Nom de l’item' => (valeur renvoyée par getImage), ... ].
     *               Voir le contrat de {@see getImage()} pour le type exact retourné (chemin, bool, etc.).
     *
     * @throws \RuntimeException En cas d’erreur réseau (appel API), d’E/S (écriture/lecture fichier)
     *                           ou de données d’item/structure manquantes.
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
     * Récupère l’image d’un item depuis Data Dragon et la place dans le répertoire public.
     *
     * Comportement :
     * - Construit l’URL Data Dragon des images d’items pour la version donnée.
     * - Si $dir est vide, calcule le répertoire cible via {@see buildDir()} en utilisant
     *   $version, $lang et self::TYPE (items).
     * - Construit les chemins absolu/relatif du fichier via {@see buildPath()}.
     * - Si le fichier existe déjà en local et que $force=false, retourne immédiatement
     *   le chemin relatif.
     * - Sinon, télécharge le binaire. S’il correspond à un fichier déjà présent (via
     *   {@see binaryExisting()}), crée un lien (hard link) vers ce fichier ; à défaut,
     *   écrit le binaire sur disque.
     *
     * @param string $name   Nom de fichier de l’image (ex. "1001.png").
     * @param string $version Version de LoL (ex. "15.1.1").
     * @param array  $dir    Tableau de chemins retourné par {@see buildDir()} :
     *                       doit contenir au minimum 'absDir' et/ou être compatible
     *                       avec {@see buildPath()}. Si vide, il sera calculé.
     * @param bool   $force  Si true, force le re-téléchargement/réécriture même si le
     *                       fichier existe déjà.
     * @param string $lang   Code de langue (ex. "fr_FR"). Utilisé uniquement si $dir est vide
     *                       afin de déterminer le répertoire cible.
     *
     * @return string Chemin relatif du fichier image dans le répertoire public.
     *
     * @throws \RuntimeException En cas d’échec réseau (appel HTTP), d’E/S (écriture/liaison),
     *                           ou si la construction des chemins échoue.
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
     * Paginer la liste des items pour une version/langue données et préparer leurs images.
     *
     * - Charge les items via {@see getData()} (champ 'data').
     * - Calcule la pagination selon $nb (items par page) et $numPage.
     * - Extrait le sous-ensemble d’items de la page courante via {@see splitJson()}.
     * - Prépare les images associées via {@see getImages()}.
     *
     * @param string $version  Version de League of Legends (ex. "15.1.1").
     * @param string $langue   Code de langue (ex. "fr_FR", "en_US").
     * @param int    $nb       Nombre d’items par page (si 0 ou > total, prend tout).
     * @param int    $numPage  Numéro de page 1‑based (si hors bornes, retombe à 1).
     *
     * @return array{
     *     items: array,                         // Sous-ensemble d’items pour la page
     *     images: array,                        // [nomItem => chemin relatif de l’image]
     *     meta: array{
     *         currentPage:int,
     *         nombrePage:int,
     *         itemPerPage:int,
     *         totalItem:int,
     *         type:string
     *     }
     * }
     *
     * @throws \RuntimeException En cas d’échec de lecture/écriture ou données invalides.
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

    /**
     * Recherche des items par leur nom (partiel ou complet), insensible à la casse,
     * pour une version et une langue données. Limite optionnelle du nombre de résultats.
     *
     * - Charge les items via {@see getData()} (champ 'data').
     * - Normalise la chaîne de recherche en minuscules.
     * - Parcourt chaque item et teste si $item['name'] contient la sous-chaîne recherchée.
     * - Ajoute la clé (ID d’item) dans chaque résultat sous la forme 'id' => <clé>, afin de
     *   pouvoir réutiliser l’identifiant de l’item.
     * - S’arrête dès que $max résultats ont été collectés (si $max > 0).
     *
     * @param string $name    Terme recherché (2 à 50 caractères).
     * @param string $version Version de League of Legends (ex. "15.1.1").
     * @param string $lang    Code de langue (ex. "fr_FR", "en_US").
     * @param int    $max     Nombre max. de résultats (0 = illimité).
     *
     * @return array<int,array> Liste d’items trouvés (chaque entrée = tableau associatif de l’item
     *                          + clé 'id' contenant l’identifiant/clé de l’item).
     *
     * @throws \InvalidArgumentException Si $name a une longueur invalide.
     * @throws \RuntimeException         Si le format des données est invalide.
     */
/*     public function searchByName(string $name, string $version, string $lang, int $max = 0): array
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
    } */

}