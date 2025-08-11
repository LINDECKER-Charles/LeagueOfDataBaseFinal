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
        // même base que ton UploadManager (ex: "%kernel.project_dir%/public")
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
        $path = $this->buildSummonersPath($version, $lang);

        // Cache local
        if ($file = $this->utils->fileIsExisting($path['absPath'])) {
            return $file;
        }

        // Fetch DDragon
        $content = $this->aPICaller->call("https://ddragon.leagueoflegends.com/cdn/{$version}/data/{$lang}/summoner.json");

        // On passe des données décodées à l'uploader pour éviter un double-encodage
        $data = $this->utils->decodeJson($content, true);

        // Sauvegarde (chemin absolu pour garantir l'emplacement)
        $this->uploader->saveJson($path['absDir'], $path['filename'], $data);

        // Retourne le même contenu que celui persisté
        $savedJson = $this->utils->encodeJson($data);

        return $savedJson;
    }

    /**
     * Construit les chemins (relatif/absolu) vers le fichier des sorts d’invocateur.
     *
     * - relDir   : chemin relatif (ex. "15.1.1/fr_FR/summoner")
     * - absDir   : chemin absolu du dossier cible (baseDir + relDir)
     * - filename : nom du fichier ("summoners.json")
     * - absPath  : chemin absolu complet vers le fichier (absDir + filename)
     *
     * NB : cette méthode ne crée pas le dossier cible ; appeler un mkdir/Filesystem au besoin.
     *
     * @param string $version Version DDragon (ex. "15.1.1")
     * @param string $lang    Langue DDragon (ex. "fr_FR")
     *
     * @return array{
     *   relDir: string,
     *   absDir: string,
     *   filename: string,
     *   absPath: string
     * }
     */
    private function buildSummonersPath(string $version, string $lang, bool $img = false): array{
        if($img){
            $relBase = $this->utils->buildDir($version, $lang, 'summoner', true);
            $relImg  = $relBase;
            $absImg  = Path::join($this->baseDir, $relImg);
            return [
                'relBase' => $relBase,
                'relImg' => $relImg,
                'absImg' => $absImg,
            ];
        }
        $relDir   = $this->utils->buildDir($version, $lang, 'summoner');
        $absDir   = Path::join($this->baseDir, $relDir);
        $filename = 'summoners.json';
        $absPath  = Path::join($absDir, $filename);
        return [
            'relDir' => $relDir,
            'absDir' => $absDir,
            'filename' => $filename,
            'absPath' => $absPath,
        ];
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
        $spells = array_values($this->utils->decodeJson($json, true)['data'] ?? []);

        // 2) Dossiers (abs/rel)
        $path = $this->buildSummonersPath($version, $lang, true);

        $this->fs->mkdir($path['absImg']);

        // 3) Boucle téléchargement
        $result = [];
        $baseUrl = "https://ddragon.leagueoflegends.com/cdn/{$version}/img/spell/";

        foreach ($spells as $s) {
            $id  = $s['id']   ?? null;
            $img = $s['image']['full'] ?? null;

            if (!$id || !$img) {
                continue;
            }

            $relPath = $path['relImg'] . '/' . $img;
            $absPath = Path::join($path['absImg'], $img);

            if (!$force && $this->fs->exists($absPath)) {
                $result[$id] = $relPath;
                continue;
            }
            
            // Télécharge binaire et écrit le fichier
            $binary = $this->aPICaller->call($baseUrl . $img); // renvoie du binaire
            if($src = $this->utils->binaryExisting($binary, $img, 'summoner')){
                /* dd($src); */
                @link($src, $absPath) /* || $this->fs->symlink($src, $absPath) */ /* || $this->fs->copy($src, $absPath, true) */;
            }else{
                /* dd('pas de li'); */
                $this->fs->dumpFile($absPath, $binary);
            }
            

            $result[$id] = $relPath;
        }

        return $result;
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
