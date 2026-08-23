<script setup lang="ts">
/**
 * Signature of the navigation loader: a charging Hextech core. Purely decorative
 * (aria-hidden) and self-contained — it owns its own animation vocabulary so the
 * overlay SFC stays about the manifest, the live line and the progress bar.
 */
</script>

<template>
    <div class="hx-core" aria-hidden="true">
        <span class="hx-core__glow"></span>
        <svg viewBox="0 0 120 120" class="hx-core__svg">
            <polygon
                class="hx-core__ring"
                points="112,60 86,105 34,105 8,60 34,15 86,15"
                fill="none"
            />
            <polygon
                class="hx-core__sweep"
                points="112,60 86,105 34,105 8,60 34,15 86,15"
                fill="none"
                pathLength="100"
            />
            <polygon class="hx-core__inner" points="98,60 79,93 41,93 22,60 41,27 79,27" />
            <g class="hx-core__orbit">
                <circle cx="60" cy="8" r="2.6" />
                <circle cx="105" cy="86" r="2.6" />
                <circle cx="15" cy="86" r="2.6" />
            </g>
            <rect class="hx-core__gem" x="50" y="50" width="20" height="20" rx="1.5" />
        </svg>
    </div>
</template>

<style scoped>
.hx-core {
    position: relative;
    width: 122px;
    height: 122px;
    margin: 0 auto 1.5rem;
}
.hx-core__glow {
    position: absolute;
    inset: -22%;
    background: radial-gradient(closest-side,
        color-mix(in srgb, var(--color-hex) 40%, transparent), transparent 70%);
    filter: blur(6px);
    animation: hx-breathe-glow 3.2s ease-in-out infinite;
}
.hx-core__svg {
    position: relative;
    width: 100%;
    height: 100%;
    overflow: visible;
}
.hx-core__ring {
    stroke: color-mix(in srgb, var(--color-gold) 45%, transparent);
    stroke-width: 1.5;
}
.hx-core__sweep {
    stroke: var(--color-hex);
    stroke-width: 2.5;
    stroke-linecap: round;
    stroke-dasharray: 22 78;
    filter: drop-shadow(0 0 6px color-mix(in srgb, var(--color-hex) 70%, transparent));
    animation: hx-sweep 1.6s linear infinite;
}
.hx-core__inner {
    fill: color-mix(in srgb, var(--color-hex) 6%, transparent);
    stroke: color-mix(in srgb, var(--color-gold) 30%, transparent);
    stroke-width: 1;
    transform-box: view-box;
    transform-origin: 60px 60px;
    animation: hx-inner 3.2s ease-in-out infinite;
}
.hx-core__orbit {
    fill: var(--color-hex-bright);
    transform-box: view-box;
    transform-origin: 60px 60px;
    animation: hx-spin 6s linear infinite;
}
.hx-core__gem {
    fill: var(--color-gold);
    stroke: var(--color-gold-bright);
    stroke-width: 1;
    transform-box: view-box;
    transform-origin: 60px 60px;
    filter: drop-shadow(0 0 5px color-mix(in srgb, var(--color-gold) 60%, transparent));
    animation: hx-gem 7s linear infinite;
}

@keyframes hx-sweep { to { stroke-dashoffset: -100; } }
@keyframes hx-spin { to { transform: rotate(360deg); } }
@keyframes hx-gem {
    0% { transform: rotate(45deg); }
    100% { transform: rotate(405deg); }
}
@keyframes hx-inner {
    0%, 100% { opacity: 0.5; transform: scale(0.97); }
    50% { opacity: 1; transform: scale(1.03); }
}
@keyframes hx-breathe-glow {
    0%, 100% { opacity: 0.55; transform: scale(0.94); }
    50% { opacity: 1; transform: scale(1.06); }
}

@media (prefers-reduced-motion: reduce) {
    .hx-core__sweep,
    .hx-core__inner,
    .hx-core__orbit,
    .hx-core__gem,
    .hx-core__glow { animation: none !important; }
}
</style>
