<?php

namespace App\Controller;

use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;

class HomeController extends AbstractController
{
    #[Route('/test', name: 'app_test')]
    public function index(): Response
    {
        return $this->render('test.html.twig');
    }
    #[Route('/setup', name: 'app_setup')]
    public function setup(): Response
    {
        return $this->render('setupPage.html.twig');
    }
}
