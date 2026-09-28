<?php
declare(strict_types=1);

namespace LoDbParity;

use App\Kernel;
use App\Service\API\AbstractManager;
use App\Service\API\ChampionManager;
use App\Service\API\DatasetRef;
use App\Service\API\ItemManager;
use App\Service\API\RuneManager;
use App\Service\API\SummonerManager;
use League\Flysystem\FilesystemOperator;
use Symfony\Bundle\FrameworkBundle\Console\Application;
use Symfony\Component\Console\Command\LazyCommand;
use Symfony\Component\Dotenv\Dotenv;

/**
 * The legacy managers and the storage they read, reached without touching app/.
 *
 * The managers are private services: they are taken from the warmup command, which holds
 * all of them, and the storage from the managers themselves. Every read goes through
 * {@see requireDataset()} or a presence check first, so that a manager never falls back to
 * a fetch through go-fetcher.
 */
final class LegacyCatalog
{
    /** The language every legacy fallback lands on (AbstractManager::FALLBACK_LANG). */
    public const FALLBACK_LANG = 'en_US';

    private const WARMUP_COMMAND = 'app:ddragon:warmup';

    /** @param array<string, AbstractManager> $managers keyed by type() */
    private function __construct(
        private readonly array $managers,
        private readonly FilesystemOperator $storage,
    ) {}

    public static function boot(string $appDir): self
    {
        (new Dotenv())->bootEnv($appDir.'/.env');
        $kernel = new Kernel((string) $_SERVER['APP_ENV'], (bool) $_SERVER['APP_DEBUG']);
        $kernel->boot();

        $command = (new Application($kernel))->find(self::WARMUP_COMMAND);
        if ($command instanceof LazyCommand) {
            $command = $command->getCommand();
        }

        $managers = [];
        foreach (self::property($command, 'managers') as $manager) {
            $managers[$manager->type()] = $manager;
        }
        $storage = self::property($managers['champion'], 'ddragonStorage', AbstractManager::class);

        return new self($managers, $storage);
    }

    public function champions(): ChampionManager
    {
        return $this->managers['champion'];
    }

    public function items(): ItemManager
    {
        return $this->managers['item'];
    }

    public function runes(): RuneManager
    {
        return $this->managers['runesReforged'];
    }

    public function summoners(): SummonerManager
    {
        return $this->managers['summoner'];
    }

    public function has(string $key): bool
    {
        return $this->storage->fileExists($key);
    }

    /**
     * The stored dataset of a type, read through its manager.
     *
     * @return array<mixed>
     * @throws NotWarmedException when the warmup never stored it
     */
    public function requireDataset(AbstractManager $manager, DatasetRef $ref): array
    {
        if (!$this->has($this->datasetKey($manager->type(), $ref->version, $ref->lang))) {
            throw new NotWarmedException(sprintf(
                'Dataset %s %s/%s is not in the legacy storage: run app:ddragon:warmup first.',
                $manager->type(),
                $ref->version,
                $ref->lang,
            ));
        }

        return $manager->getData($ref->version, $ref->lang);
    }

    /**
     * The language a stored dataset is written in, as `contentLanguage` in the new export.
     *
     * The legacy stack stores the en_US payload under the requested language when Data
     * Dragon lacks it, and an empty payload when en_US lacks it too: the stored bytes tell
     * which happened.
     */
    public function contentLanguage(string $type, string $version, string $lang): ?string
    {
        $stored = $this->storage->read($this->datasetKey($type, $version, $lang));
        if (in_array(trim($stored), ['[]', '{}', ''], true)) {
            return null;
        }

        if ($lang === self::FALLBACK_LANG) {
            return $lang;
        }

        $english = $this->datasetKey($type, $version, self::FALLBACK_LANG);

        return $this->has($english) && $this->storage->read($english) === $stored
            ? self::FALLBACK_LANG
            : $lang;
    }

    private function datasetKey(string $type, string $version, string $lang): string
    {
        return sprintf('data/%s/%s/%s.json', $version, $lang, $type);
    }

    private static function property(object $owner, string $name, ?string $class = null): mixed
    {
        return (new \ReflectionProperty($class ?? $owner::class, $name))->getValue($owner);
    }
}
