<?php
declare(strict_types=1);

namespace App\Service\API;

final class ItemManager extends AbstractManager implements CategoriesInterface
{
    protected const TYPE = 'item';

    protected function imageUrl(string $version, string $name): string
    {
        return sprintf('https://ddragon.leagueoflegends.com/cdn/%s/img/item/%s', $version, $name);
    }

    public function getByName(string $name, string $version, string $lang): array
    {
        $data = $this->dataMap($version, $lang);
        if (isset($data[$name])) {
            return $data[$name];
        }

        throw ResourceNotFoundException::forEntry(static::TYPE, $name);
    }

    /**
     * Résout une liste d'identifiants d'objets liés (item.into / item.from) en
     * entrées enrichies prêtes à lier vers leur page détail. Les IDs absents du
     * jeu de données courant (objets retirés d'un patch) sont ignorés, les
     * doublons dédupliqués, et l'ordre d'entrée (= ordre de recette) préservé.
     *
     * @param list<int|string> $ids
     * @return list<array{id: string, name: string, image: ?string, gold: ?int}>
     */
    public function resolveRelated(array $ids, string $version, string $lang): array
    {
        if ($ids === []) {
            return [];
        }

        $data = $this->getData($version, $lang)['data'] ?? [];

        $picked = [];
        foreach ($ids as $id) {
            $id = (string) $id;
            if (isset($data[$id]) && !isset($picked[$id])) {
                $picked[$id] = $data[$id];
            }
        }
        if ($picked === []) {
            return [];
        }

        // Icônes secondaires résolues via le scope de déferration ambiant : différées
        // sous un rendu de liste ({@see relatedIndex}), synchrones en détail / éditeur
        // de build / tendances (icônes réelles sur version froide). La feature (nom +
        // lien + prix) ne dépend pas de l'image, différable sans la casser.
        $files = array_values(array_filter(array_map(
            static fn (array $entry): ?string => $entry['image']['full'] ?? null,
            $picked,
        )));
        $paths = $files === [] ? [] : $this->resolveImages($version, $files);

        $result = [];
        foreach ($picked as $id => $entry) {
            $file = $entry['image']['full'] ?? null;
            $result[] = [
                // PHP recaste les clés de tableau numériques en int → on rétablit.
                'id'    => (string) $id,
                'name'  => (string) ($entry['name'] ?? ''),
                'image' => $file !== null ? ($paths[$file] ?? null) : null,
                // Coût total du composant/évolution — permet d'afficher le prix
                // sur les nœuds de l'arbre de recette sans requête additionnelle.
                'gold'  => isset($entry['gold']['total']) ? (int) $entry['gold']['total'] : null,
            ];
        }

        return $result;
    }

    /**
     * Index (id → entrée résolue) de toutes les évolutions (`into`) référencées
     * par les objets fournis, résolues en une passe. Permet à la liste d'afficher
     * nom/icône/lien réels pour chaque id d'évolution sans une résolution par carte.
     *
     * @param iterable<array<string, mixed>> $items
     * @return array<string, array{id: string, name: string, image: ?string, gold: ?int}>
     */
    public function relatedIndex(iterable $items, string $version, string $lang): array
    {
        $ids = [];
        foreach ($items as $item) {
            foreach ($item['into'] ?? [] as $id) {
                $ids[(string) $id] = true;
            }
        }
        if ($ids === []) {
            return [];
        }

        // List render: the union of every item's evolution icons is a large cold
        // batch (one per upgrade target). Defer it like paginate() defers the
        // primary icons — otherwise a non-warm patch blocks the whole /objects
        // response on this synchronous batch (the switch-version-then-navigate lag).
        // Chips stay usable (name + link + gold); icons warm after the response.
        return $this->withImageDeferral(function () use ($ids, $version, $lang): array {
            $index = [];
            foreach ($this->resolveRelated(array_keys($ids), $version, $lang) as $entry) {
                $index[$entry['id']] = $entry;
            }

            return $index;
        });
    }

    /**
     * Arbre de recette descendant : cet objet en racine, chaque composant (`from`)
     * développé récursivement jusqu'aux objets de base. Construit depuis le jeu de
     * données en cache (aucune sortie réseau pour les données) ; toutes les icônes
     * sont résolues en une seule passe. La garde `seen` (par chemin) coupe les
     * cycles tout en autorisant un même composant dans des branches sœurs (ex. deux
     * épées longues), et `maxDepth` borne les recettes pathologiques.
     *
     * @return array{id:string,name:string,image:?string,gold:?int,combine:?int,children:list<mixed>}|array{}
     */
    public function recipeTree(string $id, string $version, string $lang, int $maxDepth = 6): array
    {
        $data = $this->getData($version, $lang)['data'] ?? [];

        $files = [];
        $build = static function (string $nid, array $seen, int $depth) use (&$build, $data, $maxDepth, &$files): ?array {
            if (!isset($data[$nid]) || isset($seen[$nid]) || $depth > $maxDepth) {
                return null;
            }
            $seen[$nid] = true;
            $entry = $data[$nid];
            $file = $entry['image']['full'] ?? null;
            if ($file !== null) {
                $files[$file] = true;
            }

            $children = [];
            foreach ($entry['from'] ?? [] as $childId) {
                $node = $build((string) $childId, $seen, $depth + 1);
                if ($node !== null) {
                    $children[] = $node;
                }
            }

            return [
                'id'       => $nid,
                'name'     => (string) ($entry['name'] ?? ''),
                'file'     => $file,
                'gold'     => isset($entry['gold']['total']) ? (int) $entry['gold']['total'] : null,
                'combine'  => isset($entry['gold']['base']) ? (int) $entry['gold']['base'] : null,
                'children' => $children,
            ];
        };

        $tree = $build($id, [], 0);
        if ($tree === null) {
            return [];
        }

        $paths = $files === [] ? [] : $this->resolveImages($version, array_keys($files));

        return $this->attachRecipeImages($tree, $paths);
    }

    /**
     * Remplace le fichier d'icône brut par son URL résolue sur tout l'arbre.
     *
     * @param array<string, mixed> $node
     * @param array<string, ?string> $paths
     * @return array<string, mixed>
     */
    private function attachRecipeImages(array $node, array $paths): array
    {
        $node['image'] = $node['file'] !== null ? ($paths[$node['file']] ?? null) : null;
        unset($node['file']);
        $node['children'] = array_map(fn (array $child): array => $this->attachRecipeImages($child, $paths), $node['children']);

        return $node;
    }

    public function searchByName(string $name, string $version, string $lang, int $max = 0): array
    {
        $this->assertSearchable($name);

        $data = $this->getData($version, $lang)['data'] ?? [];
        if (!is_array($data)) {
            throw new \RuntimeException('Format de données invalide.');
        }

        $results = [];
        $search = mb_strtolower($name);
        foreach ($data as $key => $item) {
            if ($max !== 0 && count($results) >= $max) {
                break;
            }
            if (isset($item['name']) && str_contains(mb_strtolower($item['name']), $search)) {
                $results[] = array_merge($item, ['id' => $key]);
            }
        }

        return $results;
    }

    public function getImages(string $version, string $lang, bool $force = false, array $data = []): array
    {
        if (!$data) {
            $data = $this->dataList($this->getData($version, $lang));
        }

        $resolved = $this->resolveImages($version, array_keys($this->imageEntries($data)), $force);

        $result = [];
        foreach ($data as $d) {
            $id = $d['name'] ?? null;
            $img = $d['image']['full'] ?? null;
            if ($id && $img) {
                $result[$id] = $resolved[$img] ?? null;
            }
        }

        return $result;
    }
}
