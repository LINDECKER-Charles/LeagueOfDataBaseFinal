<?php
declare(strict_types=1);

namespace App\Service\Audit;

use App\Entity\User;
use App\Service\Audit\Model\AuditActor;
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

    public function resolve(?UserInterface $actor = null): AuditActor
    {
        $actor ??= $this->security->getUser();

        if ($actor instanceof User) {
            return AuditActor::user((string) $actor->getId(), $actor->displayName());
        }

        if ($actor instanceof UserInterface) {
            return AuditActor::admin($actor->getUserIdentifier());
        }

        return AuditActor::anonymous();
    }
}
