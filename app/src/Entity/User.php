<?php
declare(strict_types=1);

namespace App\Entity;

use App\Repository\UserRepository;
use Doctrine\Common\Collections\ArrayCollection;
use Doctrine\Common\Collections\Collection;
use Doctrine\ORM\Mapping as ORM;
use Symfony\Bridge\Doctrine\Validator\Constraints\UniqueEntity;
use Symfony\Component\Security\Core\User\PasswordAuthenticatedUserInterface;
use Symfony\Component\Security\Core\User\UserInterface;
use Symfony\Component\Validator\Constraints as Assert;

/**
 * Site account (favourites + builds owner). Uniqueness of email/username is
 * case-insensitive, enforced by functional LOWER() unique indexes in the
 * migration — not by plain column constraints — so the validator layer mirrors
 * that: email is normalised to lowercase in the setter, username uniqueness
 * goes through a case-insensitive repository query.
 */
#[ORM\Entity(repositoryClass: UserRepository::class)]
#[ORM\Table(name: 'users')]
#[UniqueEntity(fields: ['email'])]
#[UniqueEntity(fields: ['username'], repositoryMethod: 'findForUsernameUniqueness')]
final class User implements UserInterface, PasswordAuthenticatedUserInterface
{
    public const ROLE_DEFAULT = 'ROLE_USER';
    public const USERNAME_PATTERN = '/^[a-zA-Z0-9][a-zA-Z0-9_.-]{2,23}$/';

    #[ORM\Id]
    #[ORM\GeneratedValue]
    #[ORM\Column]
    private ?int $id = null;

    #[ORM\Column(length: 180)]
    #[Assert\NotBlank]
    #[Assert\Email]
    #[Assert\Length(max: 180)]
    private string $email = '';

    #[ORM\Column(length: 24)]
    #[Assert\NotBlank]
    #[Assert\Regex(pattern: self::USERNAME_PATTERN)]
    private string $username = '';

    /** @var list<string> */
    #[ORM\Column]
    private array $roles = [];

    /** Hashed password (never plaintext). */
    #[ORM\Column]
    private string $password = '';

    #[ORM\Column(options: ['default' => false])]
    private bool $isPublicProfile = false;

    #[ORM\Column(length: 64, nullable: true)]
    private ?string $favoriteChampionId = null;

    #[ORM\Column(length: 16, nullable: true)]
    private ?string $favoriteItemId = null;

    #[ORM\Column(length: 16, nullable: true)]
    private ?string $favoriteRuneId = null;

    #[ORM\Column(length: 64, nullable: true)]
    private ?string $favoriteSummonerId = null;

    #[ORM\Column]
    private \DateTimeImmutable $createdAt;

    /** @var Collection<int, Build> */
    #[ORM\OneToMany(targetEntity: Build::class, mappedBy: 'owner', orphanRemoval: true)]
    private Collection $builds;

    public function __construct()
    {
        $this->createdAt = new \DateTimeImmutable();
        $this->builds = new ArrayCollection();
    }

    public function getId(): ?int
    {
        return $this->id;
    }

    public function getEmail(): string
    {
        return $this->email;
    }

    public function setEmail(string $email): static
    {
        // Lowercase at the boundary so the stored value always satisfies the
        // LOWER(email) unique index and plain equality lookups stay correct.
        $this->email = mb_strtolower($email);

        return $this;
    }

    public function getUsername(): string
    {
        return $this->username;
    }

    public function setUsername(string $username): static
    {
        $this->username = $username;

        return $this;
    }

    /** @see UserInterface */
    public function getUserIdentifier(): string
    {
        return $this->email;
    }

    /**
     * @see UserInterface
     * @return list<string>
     */
    public function getRoles(): array
    {
        $roles = $this->roles;
        $roles[] = self::ROLE_DEFAULT;

        return array_values(array_unique($roles));
    }

    /** @param list<string> $roles */
    public function setRoles(array $roles): static
    {
        $this->roles = $roles;

        return $this;
    }

    /** @see PasswordAuthenticatedUserInterface */
    public function getPassword(): string
    {
        return $this->password;
    }

    public function setPassword(string $password): static
    {
        $this->password = $password;

        return $this;
    }

    public function isPublicProfile(): bool
    {
        return $this->isPublicProfile;
    }

    public function setIsPublicProfile(bool $isPublicProfile): static
    {
        $this->isPublicProfile = $isPublicProfile;

        return $this;
    }

    public function getFavoriteChampionId(): ?string
    {
        return $this->favoriteChampionId;
    }

    public function setFavoriteChampionId(?string $favoriteChampionId): static
    {
        $this->favoriteChampionId = $favoriteChampionId;

        return $this;
    }

    public function getFavoriteItemId(): ?string
    {
        return $this->favoriteItemId;
    }

    public function setFavoriteItemId(?string $favoriteItemId): static
    {
        $this->favoriteItemId = $favoriteItemId;

        return $this;
    }

    public function getFavoriteRuneId(): ?string
    {
        return $this->favoriteRuneId;
    }

    public function setFavoriteRuneId(?string $favoriteRuneId): static
    {
        $this->favoriteRuneId = $favoriteRuneId;

        return $this;
    }

    public function getFavoriteSummonerId(): ?string
    {
        return $this->favoriteSummonerId;
    }

    public function setFavoriteSummonerId(?string $favoriteSummonerId): static
    {
        $this->favoriteSummonerId = $favoriteSummonerId;

        return $this;
    }

    public function getCreatedAt(): \DateTimeImmutable
    {
        return $this->createdAt;
    }

    /** @return Collection<int, Build> */
    public function getBuilds(): Collection
    {
        return $this->builds;
    }

    public function addBuild(Build $build): static
    {
        if (!$this->builds->contains($build)) {
            $this->builds->add($build);
            $build->setOwner($this);
        }

        return $this;
    }

    public function removeBuild(Build $build): static
    {
        if ($this->builds->removeElement($build) && $build->getOwner() === $this) {
            $build->setOwner(null);
        }

        return $this;
    }

    /** @see UserInterface */
    #[\Deprecated]
    public function eraseCredentials(): void
    {
        // No transient credentials stored; method kept until Symfony 8 drops it.
    }
}
