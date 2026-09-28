// The budgets of the lot 3 milestone (L3.13), checked on the median of the runs of a page.

/** A score budget has `min` (0 to 100), a metric budget `max`, in its `unit`. */
export const BUDGETS = [
  { metric: 'performance', label: 'Performance', min: 90, unit: '' },
  { metric: 'accessibility', label: 'Accessibilité', min: 95, unit: '' },
  { metric: 'seo', label: 'SEO', min: 95, unit: '' },
  { metric: 'lcp', label: 'LCP', max: 2500, unit: 'ms' },
  { metric: 'cls', label: 'CLS', max: 0.1, unit: '' },
];

function meets(budget, value) {
  if (typeof value !== 'number') return false;
  return budget.min === undefined ? value <= budget.max : value >= budget.min;
}

/** Each budget with the page's `value` and whether it is `met`; a missing value fails. */
export function checkBudgets(metrics) {
  return BUDGETS.map((budget) => {
    const value = metrics[budget.metric];
    return { ...budget, value, met: meets(budget, value) };
  });
}
