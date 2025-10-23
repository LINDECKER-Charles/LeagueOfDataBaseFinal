<?php
declare(strict_types=1);

namespace App\Controller;

use App\Service\API\RuneManager;
use App\Service\Client\ClientManager;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;

final class RuneController extends AbstractController{
    public function __construct(
        private readonly VersionManager $versionManager, 
        private readonly ClientManager $clientManager,
        private readonly RequestStack $requestStack,
        private readonly RuneManager $runeManager,
    ) {}
}
