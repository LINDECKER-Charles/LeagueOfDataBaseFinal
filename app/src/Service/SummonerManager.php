<?php
// src/Service/SummonerManager.php
namespace App\Service;

use App\Service\Utils;
use App\Service\APICaller;
use App\Service\VersionManager;
use Symfony\Component\Filesystem\Path;
use function PHPUnit\Framework\fileExists;
use Symfony\Component\Filesystem\Filesystem;
use Symfony\Contracts\HttpClient\HttpClientInterface;

final class SummonerManager
{
    public function __construct(
        private readonly HttpClientInterface $http,
        private readonly UploadManager $uploader,
        private readonly APICaller $aPICaller,
        private readonly Utils $utils,
        private readonly string $baseDir,
        private readonly Filesystem $fs = new Filesystem(),
        private readonly VersionManager $versionManager,
    ) {}


    /* Getter */
    /**
     * Retourne le JSON des summoners (sorts d'invocateur) pour une version et une langue.
     * - Si le fichier existe localement: lit et renvoie son contenu.
     * - Sinon: récupère depuis Data Dragon, sauvegarde, puis renvoie le JSON.
     *
     * URL DDragon: https://ddragon.leagueoflegends.com/cdn/{version}/data/{lang}/summoner.json
     *
     * @param string $version Version DDragon (ex: "14.15.1").
     * @param string $lang    Langue DDragon (ex: "fr_FR").
     *
     * @return string JSON brut.
     *
     * @throws \RuntimeException En cas d'erreur réseau/lecture/encodage.
     */
    public function getSummoners(string $version, string $lang): string
    {
        $path = $this->utils->buildDirAndPath($version, $lang, 'summoner', 'summoner.json');
        
        
        // Cache local
        if ($file = $this->utils->fileIsExisting($path['absPath'])) {
            return $file;
        }
        
        // Fetch DDragon
        $data = $this->aPICaller->call("https://ddragon.leagueoflegends.com/cdn/{$version}/data/{$lang}/summoner.json");
        
        // Sauvegarde (chemin absolu pour garantir l'emplacement)
        $this->uploader->saveJson($path['absDir'], $path['fileName'], $data, true);

        return $data;
    }

    /**
     * Récupère les informations détaillées d'un sort d'invocateur par son identifiant.
     *
     * - Charge la liste complète via {@see getSummoners()}.
     * - Décode le JSON en tableau associatif.
     * - Recherche et retourne uniquement l'élément correspondant à l'ID donné.
     *
     * @param string $name    Identifiant interne du sort d'invocateur (ex: "SummonerBarrier").
     * @param string $version Version du jeu (ex: "15.12.1").
     * @param string $lang    Code de langue (ex: "fr_FR").
     *
     * @return array Tableau associatif contenant les données du sort d'invocateur.
     *
     * @throws \RuntimeException Si le format du JSON est invalide ou si aucun sort ne correspond.
     */
    public function getSummonerByName(string $name, string $version, string $lang): array{
        (string) $json = $this->getSummoners($version,$lang);

        // Décodage en tableau associatif
        (array) $data = json_decode($json, true);

        // Vérification que la clé "data" existe
        if (!isset($data['data']) || !is_array($data['data'])) {
            throw new \RuntimeException('Format de données invalide.');
        }

        // Recherche de l'invocateur par id
        foreach ($data['data'] as $summoner) {
            if (isset($summoner['id']) && $summoner['id'] === $name) {
                return $summoner;
            }
        }

        // Si non trouvé
        throw new \RuntimeException(sprintf('Aucun invocateur trouvé avec l\'ID "%s".', $name));
    }

    /**
     * Récupère les informations détaillées d'un sort d'invocateur à partir de son identifiant exact.
     *
     * - Charge la liste complète des sorts d'invocateur via {@see getSummoners()}.
     * - Décode le JSON en tableau associatif.
     * - Parcourt les données et retourne le sort dont la clé `id` correspond exactement au nom donné.
     *
     * @param string $name    Identifiant exact du sort d'invocateur (ex: "SummonerBarrier").
     * @param string $version Version du jeu (ex: "15.12.1").
     * @param string $lang    Code de langue (ex: "fr_FR").
     *
     * @return array Tableau associatif contenant les données complètes du sort d'invocateur.
     *
     * @throws \RuntimeException Si le format des données est invalide ou si aucun sort ne correspond.
     */
    public function getSummonersByName(string $name, string $version, string $lang): array{
        (string) $json = $this->getSummoners($version,$lang);

        // Décodage en tableau associatif
        $data = json_decode($json, true);

        (array) $result = [];
        // Vérification que la clé "data" existe
        if (!isset($data['data']) || !is_array($data['data'])) {
            throw new \RuntimeException('Format de données invalide.');
        }

        // Recherche de l'invocateur par id
        foreach ($data['data'] as $summoner) {
            if (isset($summoner['id']) && $summoner['id'] === $name) {
                $result = array_merge($result, $summoner);
            }
        }

        if($result){
            return $result;
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
    public function searchSummonersByName(string $name, string $version, string $lang): array
    {
        (string) $json = $this->getSummoners($version, $lang);

        // Décodage en tableau associatif
        $data = json_decode($json, true);

        if (!isset($data['data']) || !is_array($data['data'])) {
            throw new \RuntimeException('Format de données invalide.');
        }

        $results = [];
        $search = mb_strtolower($name); // normalisation pour recherche insensible à la casse

        foreach ($data['data'] as $summoner) {
            $idMatch   = isset($summoner['id']) && str_contains(mb_strtolower($summoner['id']), $search);
            $nameMatch = isset($summoner['name']) && str_contains(mb_strtolower($summoner['name']), $search);

            if ($idMatch || $nameMatch) {
                $results[] = $summoner;
            }
        }

        return $results;
    }

    /**
     * Télécharge toutes les images des summoners pour une version/langue.
     *
     * - Utilise getSummoners() (cache disque si déjà présent) pour récupérer le JSON
     * - Pour chaque sort, télécharge l'image depuis DDragon si absente localement
     * - Enregistre dans: upload/{version}/{lang}/summoner/img/{image.full}
     *
     * @param string $version Ex: "14.16.1"
     * @param string $lang    Ex: "fr_FR"
     * @param bool   $force   Si true, retélécharge même si le fichier existe (par défaut false)
     *
     * @return array<string,string> Mapping "SummonerId" => "chemin/relatif/vers/image"
     *
     * @throws \RuntimeException En cas d'erreur réseau/écriture
     */
    public function getSummonersImages(string $version, string $lang, bool $force = false, array $sums = []): array
    {

        if(!$sums){
            // 1) Récup JSON (cache + fetch)
            (string) $data = $this->getSummoners($version, $lang);
            // 2) Dossiers (abs/rel)
            (array) $sums = array_values($this->utils->decodeJson($data, true)['data'] ?? []);
        }

        $dir = $this->utils->buildDir($version, $lang, 'summoner', true);

        // 3) Boucle téléchargement
        $result = [];

        foreach ($sums as $s) {
            $id  = $s['id']   ?? null;
            $img = $s['image']['full'] ?? null;

            if (!$id || !$img) {
                continue;
            }

            $result[$id] = $this->getSummonerImage($img, $version, $dir, $force);
        }

        return $result;
    }
    
    /**
     * Récupère et stocke localement l'image d'un sort d'invocateur.
     *
     * - Construit l'URL de l'image sur l'API DDragon.
     * - Si le chemin n'est pas fourni, le génère via {@see utils::buildDirAndPath()}.
     * - Retourne l'image depuis le cache local si elle existe, sauf si `$force` est activé.
     * - Sinon, télécharge l'image depuis l'API et la sauvegarde localement.
     *
     * @param string $name   Nom de fichier de l'image (ex: "SummonerBarrier.png").
     * @param string $version Version du jeu (ex: "15.12.1").
     * @param array  $dir    Tableau de chemins généré par buildDirAndPath() (optionnel).
     * @param bool   $force  Si `true`, force le téléchargement même si l'image existe déjà.
     * @param string $lang   Code de langue (ex: "fr_FR"), nécessaire si `$dir` est vide.
     *
     * @return string Chemin relatif vers l'image enregistrée.
     *
     * @throws \RuntimeException Si le téléchargement ou la sauvegarde échoue.
     */
    public function getSummonerImage(string $name, string $version, array $dir = [], bool $force = false, string $lang = ''):string {
        $baseUrl = "https://ddragon.leagueoflegends.com/cdn/{$version}/img/spell/";

        if(!$dir){
            $dir = $this->utils->buildDirAndPath($version, $lang, 'summoner', $name, true);
        }
        $path = $this->utils->buildPath($dir, $name);
        if (!$force && $this->fs->exists($path['absPath'])) {
            return $path['relPath'];
        }

        $binary = $this->aPICaller->call($baseUrl . $name);
        if($src = $this->utils->binaryExisting($binary, $name, 'summoner')){
            @link($src, $path['absPath']);
        }else{
            $this->fs->dumpFile($path['absPath'], $binary);
        }

        return $path['relPath'];
    }

    /* Fonction de trie */
    /**
     * Décode le JSON DDragon et trie les summoners par nom (insensible à la casse).
     *
     * @param string $json JSON brut renvoyé par DDragon (summoner.json)
     * @return array<int, array<string, mixed>> Tableau indexé des sorts triés
     * @throws \JsonException Si le JSON est invalide
     */
    public function orderAcsSummoners(array $json): array
    {
        /** @var array{data?: array<string, array{name?: string}>} $decoded */
        $summoners = array_values($json['data'] ?? []);
        usort($summoners, static fn(array $a, array $b): int =>
            strcasecmp($a['name'] ?? '', $b['name'] ?? '')
        );
        return $summoners;
    }

    /**
     * Récupère (avec cache disque) puis parse+trie les summoners.
     *
     * @param string $version Ex: "14.16.1"
     * @param string $lang    Ex: "fr_FR"
     * @return array<int, array<string, mixed>>
     * @throws \RuntimeException|\JsonException
     */
    public function getSummonersParsed(string $version, string $lang): array
    {
        return json_decode($this->getSummoners($version, $lang), true)['data'];
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
     *     summoners: array,     // Liste paginée des sorts d’invocateur
     *     images: array,        // Tableau des chemins d'images associés
     *     meta: array{          // Informations de pagination
     *         currentPage: int,
     *         nombrePage: int,
     *         itemPerPage: int,
     *         totalItem: int,
     *         type: string
     *     },
     * }
     */
    public function paginateSummoners(string $version, string $langue, int $nb = 1, int $numPage = 1): array{
        (array) $json = json_decode($this->getSummoners($version, $langue), true)['data'];
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
        
        (array) $images = $this->getSummonersImages($version, $langue, false, $json);
        return [
            'summoners' =>  $json,
            'images' => $images,
            'meta' => [
                'currentPage' => $numPage,
                'nombrePage' => $ttPage,
                'itemPerPage' => $nb,
                'totalItem' => $ttSum,
                'type' => 'summoners',
            ],
        ];
    }


}
