<?php
// src/Service/RuneManager.php
namespace App\Service\API;

final class RuneManager extends AbstractManager{

    private const TYPE = 'runesReforged';

    public function getData(string $version, string $lang): array
    {
        /* dd($version, $lang); */
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

    public function getByName(string $name, string $version, string $lang): array{
        
        (array) $data = $this->getData($version,$lang);

        foreach($data as $rune){
            if($rune['key'] === $name){
                return $rune;
            }
        }

        throw new \RuntimeException(sprintf('Aucun rune trouvé avec le nom "%s".', $name));
    }

    public function getImage(string $name, string $version, array $dir = [], bool $force = false, string $lang = ''):string {
        
        $baseUrl = "https://ddragon.leagueoflegends.com/cdn/img/";
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

    public function getImages(string $version, string $lang, bool $force = false, array $data = []): array
    {
        if(!$data){
            (array) $data = array_values($this->getData($version, $lang) ?? []);
        }
        
        $dir = $this->buildDir($version, $lang, SELF::TYPE, true);

        $result = [];
        foreach ($data as $d) {
            
            $icon = $d['icon'] ?? null;
            $key  = $d['key'] ?? null;
            if (!$icon || !$key) {
                continue;
            }
            $result[$key]['icon'] = $this->getImage($icon, $version, $dir, $force);

            foreach ($d['slots'] as $index => $slot) {
                foreach ($slot['runes'] as $rune) {
                    $runeIcon = $rune['icon'] ?? null;
                    $runeKey  = $rune['key'] ?? null;
                    if (!$runeIcon || !$runeKey) {
                        continue;
                    }
                    $result[$key]['slots'][$index][$runeKey] = $this->getImage($runeIcon, $version, $dir, $force);
                }
            }
        }
        return $result;
    }

    public function paginate(string $version, string $langue, int $nb = 1, int $numPage = 1): array{

        (array) $json = $this->getData($version, $langue);

        (int) $ttSum = count($json);
        if($nb === 0 || $nb > $ttSum){
            if($ttSum > 20){
                $nb = 20;
            }else{
                $nb = $ttSum;
            }
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
        /* dd($json, $images); */
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