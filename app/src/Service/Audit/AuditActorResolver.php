<?php
declare(strict_types=1);

namespace App\Service\Audit;

use App\Entity\User;
use Symfony\Bundle\SecurityBundle\Security;
use Symfony\Component\Security\Core\User\UserInterface;

/**
 * Resolves the acting identity of an audit event from the security token. Three
 * shapes: a site account ({@see User}), the env-defined operator (any other
 * authenticated {@see UserInterface}, i.e. the admin InMemoryUser), or anonymous.
 */
final class AuditActorResolver
{
    public function __construct(private readonly Security $security) {}

    /**
     * @return array{type: string, id: ?string, label: string}
     */
    public function resolve(?UserInterface $actor = null): array
    {
        $actor ??= $this->security->getUser();

        if ($actor instanceof User) {
            return [
                'type' => AuditEvent::ACTOR_USER,
                'id' => (string) $actor->getId(),
                'label' => $actor->displayName(),
            ];
        }

        if ($actor instanceof UserInterface) {
            $identifier = $actor->getUserIdentifier();

            return ['type' => AuditEvent::ACTOR_ADMIN, 'id' => $identifier, 'label' => $identifier];
        }

        return ['type' => AuditEvent::ACTOR_ANONYMOUS, 'id' => null, 'label' => 'anonyme'];
    }
}
