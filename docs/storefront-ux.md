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

## Owner Clarification: Marketplace Navigation And Sales

The owner's follow-up screenshot and explanation supersede any interpretation that only auction-page styling was requested. Required product capabilities are:

- A prominent shared search/category bar on customer pages, rather than searching only from the home page.
- An account dropdown with real destinations appropriate to the signed-in role. Buyer wishlists, bid activity, offers and orders should become discoverable here as their complete workflows are implemented. Do not expose dead account links.
- Wishlist hearts and **multiple named wishlists**, with persistent per-buyer/per-tenant membership and management, not just one browser-local favourites array.
- Tenant-created **auction collections** such as Wednesday Auctions, Friday Auctions, Sunday Auctions or a named one-off Tuesday/customer event. These are merchandising/filter groups; the existing individual lot auctions retain their actual start/end times, bidding state and result. Do not hardcode weekly days or silently inherit/override a lot's auction schedule. Category and auction collection are different dimensions. Initially propose one optional primary collection per lot; confirm assignment cardinality before that section's migration. Keep visible collection navigation tenant-managed and show only actual public lots.
- Fixed-price buying, with optional negotiated offers that the seller can accept or decline. The owner also likes marketplaces offering bidding and a set price. The current core treats fixed price and timed auction as separate modes; changing that to simultaneous Buy Now/bidding requires explicit rules for existing bids, stock reservation and settlement. Do not simply render a Buy Now button against an active auction.
- A real shopping cart and checkout. Keep cart ownership, tenant, currency, quantity/availability and recorded delivery/charges authoritative. Auction lots are not purchasable merely because they were placed in a cart; winning orders retain their existing recorded results and verified payment journey. Combining payment across orders needs its own intent/quote/notification work, not a basket-shaped link to today's single-order payment form.
- Comfortable category discovery, search refinement and mobile operation. No general-retail categories, promotional banners, savings claims, trust badges or seller ratings are implied by the supplied reference screenshot.

Controlled sequence after the inspected-bid section: **shared header + multiple wishlists**; tenant-managed auction collections and useful filter navigation; private My bids/activity; fixed-price sales and offer lifecycle; cart/checkout integration. Keep the phone-based seller upload workflow and richer price/location filters on the active backlog, and reassess scope against pilot launch gates. Offers need agreed expiry/acceptance/reservation rules and concurrency tests; wishlist alerts need a separate consent/delivery section. None of those unimplemented controls should appear as working actions now.

The wishlist section reuses the existing bounded JSON-aggregate/revision pattern used for charge catalogues: one tenant/buyer row, up to 20 named lists, 200 entries per list and 1000 total memberships. This bounds request/database work during the pilot. A single revision protects cross-list moves from lost updates; no last-write-wins overwrite or automatic POST retry is allowed. Shared public lot projection rechecks approval/active/tenant visibility and excludes document/storage/reserve/bidder/merchant details. Unavailable saved entries expose a count only, with a cleanup action, not hidden listing IDs or old titles/photos.

Icon assets are vendored from the official [Lucide repository](https://github.com/lucide-icons/lucide/tree/0.533.0/icons), pinned to 0.533.0. Lucide and Feather licence notices ship with them; no runtime CDN or icon-library JavaScript is introduced.
