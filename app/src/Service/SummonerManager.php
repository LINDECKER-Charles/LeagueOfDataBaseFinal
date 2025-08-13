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
    public function getSummonersImages(string $version, string $lang, bool $force = false): array
    {

        // 1) Récup JSON (cache + fetch)
        $json = $this->getSummoners($version, $lang);
        
        $sums = array_values($this->utils->decodeJson($json, true)['data'] ?? []);

        // 2) Dossiers (abs/rel)
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
    
    private function getSummonerImage(string $name, string $version, array $dir, bool $force){
        $baseUrl = "https://ddragon.leagueoflegends.com/cdn/{$version}/img/spell/";

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
    public function getSummonersOrderAndParsed(string $version, string $lang): array
    {
        $json = $this->utils->decodeJson($this->getSummoners($version, $lang), true);
        return $this->orderAcsSummoners($json);
    }
    
}
