import { installConfirmSubmit } from './confirmSubmit'
import { installContactDialog } from './contactDialog'
import { installImageLifecycle } from '../images/imageLifecycle'
import { installPendingImages } from '../images/pendingImages'
import { installReveal } from './reveal'
import { installScrollspy } from './scrollspy'
import { installSectionNav } from './sectionNav'
import { installSwitcherAutoClose } from './switchers'
import { installTheme } from './theme'

/**
 * Composition root of the progressive page effects, all Turbo-safe: each module
 * owns ONE subject plus its own listeners and observers, so an effect can be
 * read, tested or dropped without touching the others. Installation order is
 * meaningful for the delegated `click` handlers — they run in that order.
 */
export function installEnhancements(): void {
    installReveal()
    installScrollspy()
    installImageLifecycle()
    installPendingImages()
    installSwitcherAutoClose()
    installTheme()
    installSectionNav()
    installContactDialog()
    installConfirmSubmit()
}
