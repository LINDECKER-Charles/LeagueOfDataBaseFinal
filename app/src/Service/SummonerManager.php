<?php
// src/Service/SummonerManager.php
namespace App\Service;

use App\Service\Utils;
use App\Service\APICaller;
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
        // même base que ton UploadManager (ex: "%kernel.project_dir%/public")
        private readonly string $baseDir,
        private readonly Filesystem $fs = new Filesystem(),
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
        $relDir   = $this->buildSummonerDir($version, $lang);
        $absDir   = Path::join($this->baseDir, $relDir);
        $filename = 'summoners.json';
        $absPath  = Path::join($absDir, $filename);

        // Cache local
        $file = $this->utils->fileIsExisting($absPath);
        if($file){
            return $file;
        }
        /* dd('ahahhaa'); */
        // Fetch DDragon
        $content = $this->aPICaller->call("https://ddragon.leagueoflegends.com/cdn/{$version}/data/{$lang}/summoner.json");

        // On passe des données décodées à l'uploader pour éviter un double-encodage
        $data = $this->utils->decodeJson($content, true);

        // Sauvegarde (chemin absolu pour garantir l'emplacement)
        $this->uploader->saveJson($absDir, $filename, $data);

        // Retourne le même contenu que celui persisté
        $savedJson = $this->utils->encodeJson($data);

        return $savedJson;
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
public function fetchSummonerImages(string $version, string $lang, bool $force = false): array
{
    // 1) Récup JSON (cache + fetch)
    $json = $this->getSummoners($version, $lang);
    $decoded = $this->utils->decodeJson($json, true);
    $spells  = array_values($decoded['data'] ?? []);

    // 2) Dossiers (abs/rel)
    $relBase = $this->buildSummonerDir($version, $lang);
    $relImg  = $relBase . '/img';
    $absImg  = Path::join($this->baseDir, $relImg);
    $this->fs->mkdir($absImg);

    // 3) Boucle téléchargement
    $result = [];
    $baseUrl = "https://ddragon.leagueoflegends.com/cdn/{$version}/img/spell/";

    foreach ($spells as $s) {
        $id  = $s['id']   ?? null;
        $img = $s['image']['full'] ?? null;

        if (!$id || !$img) {
            continue;
        }

        $relPath = $relImg . '/' . $img;
        $absPath = Path::join($absImg, $img);

        if (!$force && $this->fs->exists($absPath)) {
            $result[$id] = $relPath;
            continue;
        }

        // Télécharge binaire et écrit le fichier
        $binary = $this->aPICaller->call($baseUrl . $img); // renvoie du binaire
        $this->fs->dumpFile($absPath, $binary);

        $result[$id] = $relPath;
    }

    return $result;
}
    /**
     * Chemin relatif pour les summoners: upload/{version}/{lang}/summoner
     *
     * @param string $version
     * @param string $lang
     * @return string
     */
    private function buildSummonerDir(string $version, string $lang): string
    {
        return "upload/{$version}/{$lang}/summoner";
    }
    
    /**
     * Décode le JSON DDragon et trie les summoners par nom (insensible à la casse).
     *
     * @param string $json JSON brut renvoyé par DDragon (summoner.json)
     * @return array<int, array<string, mixed>> Tableau indexé des sorts triés
     * @throws \JsonException Si le JSON est invalide
     */
    public function parseSummoners(string $json): array
    {
        /** @var array{data?: array<string, array{name?: string}>} $decoded */
        $decoded   = json_decode($json, true, 512, JSON_THROW_ON_ERROR);
        $summoners = array_values($decoded['data'] ?? []);

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
        $json = $this->getSummoners($version, $lang);
        return $this->parseSummoners($json);
    }
    
}
