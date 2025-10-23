<?php
// src/Service/RuneManager.php
namespace App\Service\API;

use App\Service\API\CategoriesInterface;
use App\Service\Client\VersionManager;
use App\Service\Tools\APICaller;
use App\Service\Tools\UploadManager;
use Symfony\Component\Filesystem\Filesystem;
use Symfony\Contracts\HttpClient\HttpClientInterface;

final class RuneManager{
    public function __construct(
        private readonly HttpClientInterface $http,
        private readonly UploadManager $uploader,
        private readonly APICaller $aPICaller,
        private readonly Filesystem $fs = new Filesystem(),
        private readonly VersionManager $versionManager,
    ) {}

}