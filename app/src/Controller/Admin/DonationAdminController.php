<?php
declare(strict_types=1);

namespace App\Controller\Admin;

use App\Repository\DonationRepository;
use App\Repository\UserRepository;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Donation follow-up (read-only: donation rows are immutable accounting lines
 * written by the Stripe webhook). ROLE_ADMIN via the /admin firewall.
 */
#[Route('/admin')]
final class DonationAdminController extends AbstractAdminController
{
    private const SPARKLINE_DAYS = 30;

    #[Route('/donations', name: 'admin_donations', methods: ['GET'])]
    public function index(Request $request, DonationRepository $donations, UserRepository $users): Response
    {
        $page = $this->pageParam($request);
        ['donations' => $rows, 'total' => $total] = $donations->page($page, self::PER_PAGE);
        $daily = $donations->dailyTotals(self::SPARKLINE_DAYS);

        return $this->render('admin/donations.html.twig', [
            'rows' => $rows,
            'total' => $total,
            'page' => $page,
            'pages' => $this->pageCount($total),
            'kpis' => [
                'totalCents' => $donations->sumAll(),
                'count' => $total,
                'identifiedDonors' => $donations->countDistinctDonors(),
                'anonymous' => $donations->countAnonymous(),
                'supporters' => $users->countSupporters(),
            ],
            'daily' => array_values($daily),
            'dailySumCents' => array_sum($daily),
            'sparklineDays' => self::SPARKLINE_DAYS,
        ]);
    }
}
