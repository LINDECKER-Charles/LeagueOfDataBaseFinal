<?php 

namespace App\Service\API;

interface CategoriesInterface{

    /* Prototype */

    /**
     * Récupère les données pour une version et une langue données.
     *
     * @param string $version Version du jeu (ex. "15.1.1").
     * @param string $lang    Langue/locale des données (ex. "fr_FR").
     *
     * @return array Tableau associatif contenant les données récupérées.
     */
    public function getData(string $version, string $lang): array;
    /**
     * Recherche une entrée par son identifiant dans les données de la version et langue spécifiées.
     *
     * @param string $name    Identifiant recherché (ex. "SummonerBarrier").
     * @param string $version Version cible (ex. "15.1.1").
     * @param string $lang    Langue/locale des données (ex. "fr_FR").
     *
     * @return array Tableau associatif représentant l'entrée trouvée.
     *
     * @throws \RuntimeException Si aucun élément correspondant à l'identifiant n'est trouvé.
     */
    public function getByName(string $name, string $version, string $lang): array;
    /**
     * Recherche des entrées dont le nom correspond (totalement ou partiellement) à celui fourni.
     *
     * @param string $name    Nom ou fragment de nom à rechercher.
     * @param string $version Version cible (ex. "15.1.1").
     * @param string $lang    Langue/locale des données (ex. "fr_FR").
     * @param int    $max     Nombre maximum de résultats à retourner (0 = illimité).
     *
     * @return array Liste des entrées trouvées, sous forme de tableaux associatifs.
     *
     * @throws \RuntimeException Si la récupération des données échoue.
     */
    public function searchByName(string $name, string $version, string $lang, int $max = 0): array;
    /**
     * Récupère l'image associée à une ressource du Data Dragon (champion, sort, objet, etc.).
     *
     * Cette méthode est abstraite : chaque Manager concret (ex: SummonerManager, ItemManager)
     * doit implémenter sa propre logique pour construire le chemin et télécharger/charger l'image.
     *
     * @param string $name   Nom ou identifiant de la ressource (ex. "SummonerFlash", "InfinityEdge").
     * @param string $version Version du jeu à utiliser (ex. "15.1.1").
     * @param array  $dir     Chemin(s) de répertoires personnalisés pour stocker l'image localement.
     *                        Si vide, un chemin par défaut sera utilisé.
     * @param bool   $force   Si true, force le téléchargement même si l'image existe déjà en cache.
     * @param string $lang    Langue à utiliser (ex. "fr_FR"). Peut être ignorée si non pertinente pour l'image.
     *
     * @return string Chemin absolu ou relatif de l'image disponible localement.
     */
    public function getImage(string $name, string $version, array $dir = [], bool $force = false, string $lang = ''):string;
    /**
     * Récupère l'ensemble des images liées à une ressource (ex. les invocations).
     *
     * Chaque Manager concret doit implémenter sa propre logique pour déterminer
     * quelles ressources récupérer (champions, objets, sorts, etc.).
     *
     * @param string $version Version du jeu à utiliser (ex. "15.1.1").
     * @param string $lang    Langue à utiliser (ex. "fr_FR").
     * @param bool   $force   Si true, force le téléchargement même si les images existent déjà en cache.
     * @param array  $data    Liste optionnelle des ressources ciblées (ex. liste d'IDs). 
     *                        Si vide, toutes les ressources disponibles seront traitées.
     *
     * @return array Tableau associatif des ressources et de leur chemin local d'image.
     *               Clé = identifiant (ex. "SummonerFlash"), 
     *               Valeur = chemin vers l'image correspondante.
     */
    public function getImages(string $version, string $lang, bool $force = false, array $data = []): array;
    /**
     * Paginateur générique pour une ressource (ex. Summoner Spells, Items, Champions).
     *
     * Cette méthode doit être implémentée par chaque Manager concret. 
     * Elle permet de calculer la pagination des données pour une version et une langue données,
     * et de retourner les éléments de la page courante avec leurs images associées.
     *
     * @param string $version  Version du jeu à utiliser (ex. "15.1.1").
     * @param string $langue   Code de langue à utiliser (ex. "fr_FR").
     * @param int    $nb       Nombre d’éléments par page. Si 0 ou supérieur au total, tout est affiché.
     * @param int    $numPage  Numéro de la page à afficher (1 par défaut).
     *
     * @return array{
     *     <TYPE>s: string,    // Nom du type de ressource (défini par SELF::TYPE)
     *     images: array,       // Tableau des chemins d'images associés
     *     meta: array{         // Informations de pagination
     *         currentPage: int,
     *         nombrePage: int,
     *         itemPerPage: int,
     *         totalItem: int,
     *         type: string
     *     },
     * }
     */
    public function paginate(string $version, string $langue, int $nb = 1, int $numPage = 1): array;
}