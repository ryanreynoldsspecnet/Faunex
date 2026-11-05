# Storefront UX Direction

## Reference Review: 5 November 2026

The owner supplied [Bobshop](https://www.bobshop.co.za/) as a reference for comfortable discovery, filtering and wishlists, not a request to copy its branding or general-retail navigation. Its public homepage exposes a prominent search/category entry and separate buying/selling/account navigation. Its [finding-items guide](https://www.bobshop.co.za/help/help-buying-finding-items/361) describes category-scoped search and refinement. Its [buyer guide](https://help.bobshop.co.za/portal/en/kb/articles/buying-on-bob-shop-your-complete-guide) distinguishes finding, watching, inspecting and bidding, with photos, description and shipping information before purchase.

Evidence boundary: public homepage and official help content were reviewed. The plant-category page returned 403 to the research fetch. No authenticated Bobshop account, watchlist persistence, personal recommendations, checkout or alert delivery was exercised. This is a pattern review, not a full functional audit or proof of the competitor's implementation. No branding, copy, assets or bidding rules are copied into Faunex.

## Stormbird Sequence

1. **Discover (PR #47):** one catalogue-first tenant home/browse page, genuine public lot photos, keyword/location search, category and auction-state filters, stable sorting and pagination. Approved/active/host checks apply to data and media. This is already deployed; no made-up lots or activity should fill a sparse catalogue.
2. **Inspect and bid (current section):** put the supplied photos beside the actual lot price, state, dates and minimum bid; keep description and recorded charges readable; separate entering/reviewing/confirming a bid. Preserve actual server validation and privacy-safe bid history. Failed status reads pause the page's bidding; uncertain submissions require checking before another attempt. An exact bid is not a proxy maximum.
3. **Return to favourites (next bounded section):** persistent, private buyer watchlist with save/remove controls on catalogue and detail, a header/account destination, paging and actual current auction states. Scope every record and read to buyer plus tenant; custom-domain aliases for the same tenant share the account list. Recheck lot visibility when reading; withdrawn/inactive lots must not leak through saved records or photos. Saving is not bidding or reserving. Do not show a decorative heart before persistence, authorization, failure/retry and cross-tenant tests exist. Anonymous users need a safe sign-in continuation. Email/SMS alerts require a separate consent/delivery section, not an implied promise.
4. **Refine efficiently:** relevant category counts, explicit location and price-range filters, visible applied filters and individual clear actions. Define whether price means starting, fixed or current bid before adding price inputs; currencies must never be silently mixed. Apply filtering before counting/paging, preserve shareable URLs and support useful phone layouts. Saved searches can follow once filter semantics are stable.
5. **List from a phone:** finish seller creation/edit/photo ordering, validation and upload feedback with the same simple approach. Do not imply all animal/plant listing types are implemented because the catalogue can display them.

## Design Rules

- Readable fixed-size text, visible labels/focus, large touch targets and predictable navigation for first-time and older buyers.
- Lot photography, prices and timing take precedence over oversized generic marketing imagery.
- No fake stock imagery, counters, countdowns, ratings, reserve disclosure or unsupported actions.
- Delivery/collection, required and optional charges stay visibly distinct. The accepted checkout quote remains authoritative; a bid is not advertised as an all-inclusive total.
- Server time/state and API acceptance remain authoritative. A browser confirmation or estimated deadline never settles an auction or proves a payment.
- Platform administration stays separate from customer storefront navigation. White-label improvements must work for every tenant without hardcoded Stormbird rules.

Launch gates, backup deferral and hosted payment exception acceptance remain in [pilot-delivery.md](pilot-delivery.md). Visual polish does not close them.
