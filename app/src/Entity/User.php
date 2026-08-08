<?php
declare(strict_types=1);

namespace App\Entity;

use App\Entity\Concern\PinsProfileFavorites;
use App\Entity\Concern\TracksModerationBan;
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
    use PinsProfileFavorites;
    use TracksModerationBan;

    public const ROLE_DEFAULT = 'ROLE_USER';
    /** Username bounds — the pattern, the column and UsernameAllocator all derive from them. */
    public const USERNAME_MIN_LENGTH = 3;
    public const USERNAME_MAX_LENGTH = 24;
    public const USERNAME_PATTERN = '/^[a-zA-Z0-9][a-zA-Z0-9_.-]{'
        .(self::USERNAME_MIN_LENGTH - 1).','.(self::USERNAME_MAX_LENGTH - 1).'}$/';
    // Riot ID tagline (the part after the #): 3-5 alphanumerics, per Riot's format.
    public const RIOT_TAGLINE_PATTERN = '/^[A-Za-z0-9]{3,5}$/';

    #[ORM\Id]
    #[ORM\GeneratedValue]
    #[ORM\Column]
    private ?int $id = null;

    #[ORM\Column(length: 180)]
    #[Assert\NotBlank]
    #[Assert\Email]
    #[Assert\Length(max: 180)]
    private string $email = '';

    #[ORM\Column(length: self::USERNAME_MAX_LENGTH)]
    #[Assert\NotBlank]
    #[Assert\Regex(pattern: self::USERNAME_PATTERN)]
    private string $username = '';

    /** @var list<string> */
    #[ORM\Column]
    private array $roles = [];

    /** Hashed password (never plaintext); null for OAuth-only accounts until they set one. */
    #[ORM\Column(nullable: true)]
    private ?string $password = null;

    /** Stable Google OpenID `sub` claim — opaque id, case-sensitive equality is correct. */
    #[ORM\Column(length: 30, unique: true, nullable: true)]
    private ?string $googleId = null;

    /** Display-only Riot ID suffix (username#TAG); not unique, `#` keeps it out of URLs. */
    #[ORM\Column(length: 5, nullable: true)]
    #[Assert\Regex(pattern: self::RIOT_TAGLINE_PATTERN)]
    private ?string $riotTagline = null;

    #[ORM\Column(options: ['default' => false])]
    private bool $isPublicProfile = false;

    /** Set once by the Stripe webhook when a donation is linked to the account; never unset. */
    #[ORM\Column(options: ['default' => false])]
    private bool $isSupporter = false;

    /**
     * Email ownership proven via the signed confirmation link. Google accounts
     * start verified (Google asserts email_verified); local sign-ups start false
     * and flip once — never back — when the confirmation link is followed.
     */
    #[ORM\Column(options: ['default' => false])]
    private bool $isVerified = false;

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
    public function getPassword(): ?string
    {
        return $this->password;
    }

    public function setPassword(?string $password): static
    {
        $this->password = $password;

        return $this;
    }

    /** OAuth-only accounts have none until they set one (profile "set a password"). */
    public function hasPassword(): bool
    {
        return $this->password !== null;
    }

    /**
     * True once linked to a Google identity — its `sub` claim, not a password,
     * is the credential of record.
     */
    public function isGoogleAccount(): bool
    {
        return $this->googleId !== null;
    }

    public function getGoogleId(): ?string
    {
        return $this->googleId;
    }

    public function setGoogleId(?string $googleId): static
    {
        $this->googleId = $googleId;

        return $this;
    }

    public function getRiotTagline(): ?string
    {
        return $this->riotTagline;
    }

    public function setRiotTagline(?string $riotTagline): static
    {
        $this->riotTagline = $riotTagline;

        return $this;
    }

    /** Public-facing name: `username` or `username#TAG` when the Riot tagline is set. */
    public function displayName(): string
    {
        return $this->riotTagline === null
            ? $this->username
            : $this->username.'#'.$this->riotTagline;
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

    public function isSupporter(): bool
    {
        return $this->isSupporter;
    }

    public function setIsSupporter(bool $isSupporter): static
    {
        $this->isSupporter = $isSupporter;

        return $this;
    }

    public function isVerified(): bool
    {
        return $this->isVerified;
    }

    public function setIsVerified(bool $isVerified): static
    {
        $this->isVerified = $isVerified;

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
