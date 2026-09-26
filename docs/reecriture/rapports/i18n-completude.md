# Complétude des traductions

Produit par `npm --prefix src/LoDb.Web run i18n:report` à partir des catalogues
Transloco de `src/LoDb.Web/public/i18n/` : ne pas modifier à la main.

La référence est `en` : à l'exécution, chaque clé manquante s'y replie.
Le rapport ne bloque rien ; le job `i18n` de la CI le produit pour rendre les trous visibles.

## Synthèse

21 locales, 4 portées (racine, about, api, seo), 878 clés dans la référence `en`. Locales complètes : 2 ; incomplètes : 19.

| Locale | Traduites | Manquantes | Complétude | racine | about | api | seo |
|---|---:|---:|---:|---:|---:|---:|---:|
| ar | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| cs | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| de | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| el | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| en | 878 / 878 | 0 | 100,0 % | 0 | 0 | 0 | 0 |
| es | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| fr | 878 / 878 | 0 | 100,0 % | 0 | 0 | 0 | 0 |
| hu | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| id | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| it | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| ja | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| ko | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| pl | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| pt | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| ro | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| ru | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| th | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| tr | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| vi | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| zh-hans | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |
| zh-hant | 471 / 878 | 407 | 53,6 % | 407 | 0 | 0 | 0 |

## Clés manquantes

### racine

Clés manquantes : 407, dans ar, cs, de, el, es, hu, id, it, ja, ko, pl, pt, ro, ru, th, tr, vi, zh-hans, zh-hant.

<details>
<summary>Liste des clés</summary>

- `item.detail.gold_ledger`
- `item.detail.not_purchasable`
- `champion.detail.chromas_available`
- `champion.detail.video_notice`
- `champion.detail.icon_version`
- `champion.detail.charges`
- `champion.detail.ranks`
- `champion.detail.tips`
- `champion.detail.level`
- `champion.detail.stats.resource_regen`
- `rune.detail.full_description`
- `detail.nav.label`
- `detail.nav.sections`
- `detail.nav.previous`
- `detail.nav.next`
- `detail.nav.back_to_list`
- `filter.results`
- `filter.page`
- `filter.gauge`
- `filter.empty`
- `filter.empty_title`
- `filter.empty_cta`
- `filter.clear`
- `filter.clear_all`
- `filter.prev`
- `filter.next`
- `filter.per_page`
- `filter.all`
- `filter.filters`
- `filter.close`
- `filter.show_results`
- `filter.edition`
- `filter.match_mode`
- `filter.match_any`
- `filter.match_all`
- `filter.min`
- `filter.max`
- `filter.copy_link`
- `filter.copied`
- `filter.copy_error`
- `filter.active`
- `facet.group.profile`
- `facet.group.ratings`
- `facet.group.base_stats`
- `facet.group.identity`
- `facet.group.availability`
- `facet.group.economy`
- `facet.group.stats`
- `facet.champion.role`
- `facet.champion.roles.fighter`
- `facet.champion.roles.tank`
- `facet.champion.roles.mage`
- `facet.champion.roles.assassin`
- `facet.champion.roles.marksman`
- `facet.champion.roles.support`
- `facet.champion.resource`
- `facet.champion.resource_none`
- `facet.champion.range`
- `facet.champion.ranges.melee`
- `facet.champion.ranges.ranged`
- `facet.champion.difficulty`
- `facet.champion.attack`
- `facet.champion.defense`
- `facet.champion.magic`
- `facet.item.tag`
- `facet.item.edition`
- `facet.item.map`
- `facet.item.tier`
- `facet.item.tiers.component`
- `facet.item.tiers.epic`
- `facet.item.tiers.legendary`
- `facet.item.purchasable`
- `facet.item.consumable`
- `facet.item.price`
- `facet.summoner.mode`
- `facet.summoner.edition`
- `facet.summoner.level`
- `facet.summoner.cooldown`
- `facet.rune.path`
- `facet.rune.slot`
- `facet.rune.slot_keystone`
- `facet.rune.slot_row`
- `map.11`
- `map.12`
- `map.21`
- `map.30`
- `map.33`
- `map.35`
- `edition.all`
- `edition.modern`
- `edition.classic`
- `edition.classic_hint`
- `edition.classic_notice`
- `edition.counterpart.modern`
- `edition.counterpart.classic`
- `footer.legal_title`
- `footer.contact.repo`
- `contact.form.cta`
- `contact.form.eyebrow`
- `contact.form.title`
- `contact.form.intro`
- `contact.form.category`
- `contact.form.category_bug`
- `contact.form.category_feedback`
- `contact.form.category_review`
- `contact.form.category_commercial`
- `contact.form.name`
- `contact.form.email`
- `contact.form.subject`
- `contact.form.message`
- `contact.form.submit`
- `contact.form.cancel`
- `contact.form.close`
- `contact.flash.sent`
- `contact.flash.throttled`
- `contact.flash.error`
- `contact.error.category`
- `contact.error.email`
- `contact.error.message`
- `contact.error.message_short`
- `contact.error.message_long`
- `perf.title`
- `perf.server`
- `perf.client`
- `header.switcher.label`
- `nav.short.home`
- `nav.short.champions`
- `nav.short.items`
- `nav.short.runes`
- `nav.short.summoners`
- `nav.codex`
- `nav.account`
- `nav.login`
- `nav.register`
- `nav.profile`
- `nav.builds`
- `nav.donate`
- `nav.changelog`
- `changelog.eyebrow`
- `changelog.title`
- `changelog.lede`
- `changelog.current`
- `changelog.empty`
- `changelog.meta.title`
- `changelog.meta.description`
- `changelog.type.major`
- `changelog.type.minor`
- `changelog.type.hotfix`
- `changelog.section.fixes`
- `auth.eyebrow`
- `auth.login.title`
- `auth.login.identifier`
- `auth.login.password`
- `auth.login.remember`
- `auth.login.submit`
- `auth.login.no_account`
- `auth.login.forgot`
- `auth.register.title`
- `auth.register.email`
- `auth.register.username`
- `auth.register.username_help`
- `auth.register.terms`
- `auth.register.submit`
- `auth.register.have_account`
- `auth.flash.registered`
- `auth.flash.verify_sent`
- `auth.flash.too_many_attempts`
- `auth.flash.verify_success`
- `auth.flash.verify_error`
- `auth.flash.verify_already`
- `auth.flash.reset_sent`
- `auth.flash.reset_success`
- `auth.flash.reset_error`
- `auth.verify.banner`
- `auth.verify.banner_cta`
- `auth.verify.resend_done`
- `auth.verify.resend_throttled`
- `auth.verify.gate_build`
- `auth.verify.gate_api`
- `auth.reset.request_title`
- `auth.reset.request_lede`
- `auth.reset.email`
- `auth.reset.submit_request`
- `auth.reset.back_to_login`
- `auth.reset.check_email_title`
- `auth.reset.check_email_lede`
- `auth.reset.reset_title`
- `auth.reset.submit_reset`
- `auth.reset.invalid_token`
- `auth.logout`
- `auth.profile_stub.placeholder`
- `legal.eyebrow`
- `legal.updated`
- `legal.toc`
- `legal.notice.title`
- `legal.privacy.title`
- `legal.terms.title`
- `legal.cookies.title`
- `profile.eyebrow`
- `profile.identity.email`
- `profile.identity.member_since`
- `profile.visibility.label`
- `profile.visibility.help`
- `profile.visibility.view_public`
- `profile.visibility.state_public`
- `profile.visibility.state_private`
- `profile.builds.label`
- `profile.builds.manage`
- `profile.favorites.title`
- `profile.favorites.empty`
- `profile.favorites.unavailable`
- `profile.favorites.version_label`
- `profile.favorites.version_auto`
- `profile.favorites.version_apply`
- `profile.slot.champion`
- `profile.slot.item`
- `profile.slot.rune`
- `profile.slot.summoner`
- `profile.slot.skin`
- `profile.save`
- `profile.autosave.saving`
- `profile.autosave.saved`
- `profile.autosave.error`
- `profile.autosave.dropped`
- `profile.picker.search`
- `profile.picker.remove`
- `profile.picker.loading`
- `profile.picker.error`
- `profile.picker.retry`
- `profile.skin.change`
- `profile.skin.empty`
- `profile.skin.choose_champion`
- `profile.skin.choose_skin`
- `profile.skin.back`
- `profile.preview.button`
- `profile.preview.badge`
- `profile.preview.note_public`
- `profile.preview.note_private`
- `profile.preview.back`
- `profile.danger.title`
- `profile.danger.help`
- `profile.danger.password`
- `profile.danger.confirm_phrase`
- `profile.danger.confirm_label`
- `profile.danger.delete`
- `profile.flash.saved`
- `profile.flash.favorite_unavailable`
- `profile.flash.skin_unavailable`
- `profile.flash.wrong_password`
- `profile.flash.wrong_phrase`
- `profile.flash.deleted`
- `profile.flash.csrf`
- `profile.public.eyebrow`
- `profile.public.description`
- `profile.public.builds_count`
- `profile.public.builds_title`
- `profile.public.no_builds`
- `donate.eyebrow`
- `donate.title`
- `donate.intro`
- `donate.pledge`
- `donate.tiers.legend`
- `donate.tier.spark`
- `donate.tier.gem`
- `donate.tier.crest`
- `donate.tier.relic`
- `donate.custom.label`
- `donate.custom.placeholder`
- `donate.custom.hint`
- `donate.submit`
- `donate.product_name`
- `donate.unavailable.title`
- `donate.unavailable.body`
- `donate.legal.secure`
- `donate.legal.secure_short`
- `donate.legal.privacy`
- `donate.legal.terms`
- `donate.error.csrf`
- `donate.error.unavailable`
- `donate.error.invalid_amount`
- `donate.error.gateway`
- `donate.error.throttled`
- `donate.success.eyebrow`
- `donate.success.title`
- `donate.success.body`
- `donate.success.receipt`
- `donate.success.back`
- `donate.cancel.eyebrow`
- `donate.cancel.title`
- `donate.cancel.body`
- `donate.cancel.retry`
- `donate.cancel.back`
- `build.list.eyebrow`
- `build.list.title`
- `build.list.empty`
- `build.list.new`
- `build.list.cta_first`
- `build.list.edit`
- `build.list.delete`
- `build.list.delete_confirm`
- `build.list.public`
- `build.list.private`
- `build.list.updated`
- `build.import.to`
- `build.import.action`
- `build.import.hint`
- `build.import.done`
- `build.import.champion_missing`
- `build.import.runes_reset`
- `build.import.items_dropped`
- `build.editor.eyebrow`
- `build.editor.title_create`
- `build.editor.title_edit`
- `build.editor.identity`
- `build.editor.name`
- `build.editor.name_placeholder`
- `build.editor.description`
- `build.editor.public`
- `build.editor.submit_create`
- `build.editor.submit_update`
- `build.editor.back`
- `build.editor.nojs`
- `build.editor.loading`
- `build.editor.error`
- `build.editor.retry`
- `build.editor.ghost`
- `build.editor.counter`
- `build.editor.context.language`
- `build.editor.champion.title`
- `build.editor.champion.search`
- `build.editor.champion.empty`
- `build.editor.champion.selected`
- `build.editor.champion.open`
- `build.editor.champion.close`
- `build.editor.runes.title`
- `build.editor.runes.primary`
- `build.editor.runes.secondary`
- `build.editor.runes.keystone`
- `build.editor.runes.slot`
- `build.editor.runes.secondary_hint`
- `build.editor.steps.title`
- `build.editor.steps.add`
- `build.editor.steps.remove`
- `build.editor.steps.move_up`
- `build.editor.steps.move_down`
- `build.editor.steps.label`
- `build.editor.steps.note`
- `build.editor.steps.search_item`
- `build.editor.steps.item_empty`
- `build.editor.steps.remove_item`
- `build.editor.steps.gold`
- `build.editor.steps.preset.start`
- `build.editor.steps.preset.first_back`
- `build.editor.steps.preset.core`
- `build.editor.steps.preset.situational`
- `build.editor.steps.preset.final`
- `build.editor.armory.title`
- `build.editor.armory.add_cta`
- `build.editor.armory.done`
- `build.editor.armory.close`
- `build.editor.armory.added`
- `build.editor.armory.in_step`
- `build.editor.armory.full`
- `build.editor.armory.categories.all`
- `build.editor.armory.categories.attack`
- `build.editor.armory.categories.magic`
- `build.editor.armory.categories.defense`
- `build.editor.armory.categories.mobility`
- `build.editor.armory.categories.utility`
- `build.show.by`
- `build.show.patch`
- `build.show.runes`
- `build.show.keystone`
- `build.show.order`
- `build.show.step_cost`
- `build.show.total`
- `build.show.missing`
- `build.show.meta_fallback`
- `build.show.copy`
- `build.show.copied`
- `build.show.copy_error`
- `build.flash.created`
- `build.flash.updated`
- `build.flash.deleted`
- `build.error.csrf`
- `build.error.name.length`
- `build.error.description.length`
- `build.error.structure.invalid`
- `build.error.champion.unknown`
- `build.error.catalog_unavailable`
- `build.error.language.unknown`
- `build.error.runes.primary_style`
- `build.error.runes.primary_selection_count`
- `build.error.runes.primary_selection_slot`
- `build.error.runes.secondary_style`
- `build.error.runes.secondary_same_style`
- `build.error.runes.secondary_selection_count`
- `build.error.runes.secondary_selection_slot`
- `build.error.runes.secondary_same_slot`
- `build.error.steps.count`
- `build.error.steps.label`
- `build.error.steps.note`
- `build.error.steps.items_count`
- `build.error.steps.item_unknown`
- `build.error.steps.total_items`
- `community.trends.filter.language`
- `community.trends.filter.all_languages`

</details>

## Fichiers absents

Aucun.

## Clés hors référence

Aucun.

## Messages ICU invalides

Aucun.
