/** Close any open header switcher popover when interacting elsewhere. */
export function installSwitcherAutoClose(): void {
    document.addEventListener('click', closeSwitchersOutside)
}

function closeSwitchersOutside(event: Event): void {
    for (const details of document.querySelectorAll<HTMLDetailsElement>('details.switcher[open]')) {
        if (event.target instanceof Node && !details.contains(event.target)) {
            details.open = false
        }
    }
}
