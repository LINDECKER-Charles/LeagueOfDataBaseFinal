/**
 * Admin panel entry point. Served as a static ES module (nginx `try_files`), NOT
 * through the Vite build — same rationale as admin.css: the ops panel must keep
 * working when the public front build is broken.
 *
 * Module scripts are deferred, so the document is already parsed here.
 */

import { enhanceCharts } from './chart.js'
import { loadPanels } from './panels.js'

loadPanels()
enhanceCharts()
