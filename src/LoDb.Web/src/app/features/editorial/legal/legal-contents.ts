import type { SectionLink } from '../../../ui/navigation/section-link';
import type { LegalLanguage } from './legal-language';
import type { LegalPageId } from './legal-page-id';

/**
 * The contents of each legal text: the id of each of its sections and its numbered title,
 * in the text's language. A section added to a text is added here.
 */
export const LEGAL_CONTENTS: Record<LegalPageId, Record<LegalLanguage, readonly SectionLink[]>> = {
  notice: {
    fr: [
      { id: 'editeur', label: '1. Éditeur du site' },
      { id: 'hebergement', label: '2. Hébergement' },
      { id: 'propriete-intellectuelle', label: '3. Propriété intellectuelle' },
      { id: 'riot', label: '4. Avertissement Riot Games' },
      { id: 'donnees', label: '5. Données personnelles et cookies' },
      { id: 'contact', label: '6. Contact' },
    ],
    en: [
      { id: 'publisher', label: '1. Site publisher' },
      { id: 'hosting', label: '2. Hosting' },
      { id: 'intellectual-property', label: '3. Intellectual property' },
      { id: 'riot', label: '4. Riot Games disclaimer' },
      { id: 'data', label: '5. Personal data and cookies' },
      { id: 'contact', label: '6. Contact' },
    ],
  },
  privacy: {
    fr: [
      { id: 'responsable', label: '1. Responsable du traitement' },
      { id: 'traitements', label: '2. Traitements mis en œuvre' },
      { id: 'audience', label: "3. Mesure d'audience en détail" },
      { id: 'droits', label: '4. Vos droits' },
      { id: 'destinataires', label: '5. Destinataires et sous-traitants' },
      { id: 'transferts', label: '6. Transferts hors Union européenne' },
      { id: 'securite', label: '7. Sécurité' },
      { id: 'evolution', label: '8. Évolution de la politique' },
    ],
    en: [
      { id: 'controller', label: '1. Data controller' },
      { id: 'processing', label: '2. Processing activities' },
      { id: 'audience', label: '3. Audience measurement in detail' },
      { id: 'rights', label: '4. Your rights' },
      { id: 'recipients', label: '5. Recipients and processors' },
      { id: 'transfers', label: '6. Transfers outside the EU' },
      { id: 'security', label: '7. Security' },
      { id: 'changes', label: '8. Changes to this policy' },
    ],
  },
  terms: {
    fr: [
      { id: 'objet', label: '1. Objet et acceptation' },
      { id: 'acces', label: '2. Accès au service' },
      { id: 'compte', label: '3. Compte utilisateur' },
      { id: 'contenus', label: '4. Contenus créés par les utilisateurs' },
      { id: 'visibilite', label: '5. Visibilité des builds' },
      { id: 'dons', label: '6. Dons' },
      { id: 'propriete', label: '7. Propriété intellectuelle' },
      { id: 'responsabilite', label: '8. Disponibilité et responsabilité' },
      { id: 'resiliation', label: '9. Résiliation' },
      { id: 'droit', label: '10. Droit applicable' },
      { id: 'modification', label: '11. Modification des CGU' },
    ],
    en: [
      { id: 'purpose', label: '1. Purpose and acceptance' },
      { id: 'access', label: '2. Access to the service' },
      { id: 'account', label: '3. User account' },
      { id: 'content', label: '4. User-generated content' },
      { id: 'visibility', label: '5. Build visibility' },
      { id: 'donations', label: '6. Donations' },
      { id: 'ip', label: '7. Intellectual property' },
      { id: 'liability', label: '8. Availability and liability' },
      { id: 'termination', label: '9. Termination' },
      { id: 'law', label: '10. Governing law' },
      { id: 'changes', label: '11. Changes to these terms' },
    ],
  },
  cookies: {
    fr: [
      { id: 'approche', label: '1. Notre approche' },
      { id: 'cookies', label: '2. Cookies utilisés' },
      { id: 'consentement', label: '3. Consentement' },
      { id: 'stripe', label: '4. Paiement Stripe' },
      { id: 'pwa', label: '5. Application installable (PWA)' },
      { id: 'gestion', label: '6. Gérer ou supprimer' },
    ],
    en: [
      { id: 'approach', label: '1. Our approach' },
      { id: 'cookies', label: '2. Cookies we use' },
      { id: 'consent', label: '3. Consent' },
      { id: 'stripe', label: '4. Stripe checkout' },
      { id: 'pwa', label: '5. Installable app (PWA)' },
      { id: 'manage', label: '6. Managing or deleting' },
    ],
  },
};
