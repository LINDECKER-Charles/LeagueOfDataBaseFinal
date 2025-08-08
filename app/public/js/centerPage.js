export default function initAddonScript() {
     console.log("Script centerPage.js chargé ✅");
    const element = document.getElementById('content');
    if (element) {
        element.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }

}