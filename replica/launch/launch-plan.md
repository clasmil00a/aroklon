# Launch Plan — TokoKasir

## Pre-Launch

### Waitlist / Beta List

- Create a simple landing page (see `landing.md`) with an email capture field.
- Target: 50 beta users before public launch.
- Recruit from: Reddit r/POS, small business Facebook groups, local bakery/food shop communities.
- Offer: Lifetime free access for beta users.

### Analytics and Error Tracking

- Install error tracking (Serilog is already integrated — configure file sink).
- Track: app starts, completed sales, errors, feature usage.
- Set up alerts for crash rate > 1%.

### Where the Original's Unhappy Users Talk

From `replica/reviews.csv`:

| Community | Thread | Topic |
|-----------|--------|-------|
| Reddit r/POS | https://www.reddit.com/r/POS/comments/1qkf3vz/aronium_pro/ | Remote stock updates locked behind Pro |
| Reddit r/POS | https://www.reddit.com/r/POS/comments/1opzpbo/help_to_find_good_pos/ | Laggy SQL server, multi-location issues |
| Reddit r/POS | https://www.reddit.com/r/POS/comments/1msqjcs/need_help_with_aronium/ | Forced product entry before selling |
| Reddit r/POS | https://www.reddit.com/r/POS/comments/1p2e3gj/i_need_an_free_offline_pos_system_for_a_small/ | Kitchen ticket printing |
| Reddit r/POS | https://www.reddit.com/r/POS/comments/1n4ohd3/aronium_pos_bundles/ | Slow and unresponsive |
| Reddit r/southafrica | https://www.reddit.com/r/southafrica/comments/qg9fbq/aronium_free_pos_system/ | General POS discussion |

**Engagement strategy:** Reply to threads with a genuine, non-spammy comment mentioning TokoKasir as a free alternative that addresses their specific pain point. Do not use the original's name in keywords or ads.

### Launch Post (Product Hunt / Hacker News)

**Lead with the fix:**

> TokoKasir — a free, local-first Windows POS for small shops. No SQL server lag, no Pro paywalls, no forced product entry. Open and sell immediately.

**Key points:**
- Free forever (no subscriptions)
- Local-first (offline, no server)
- Quick sell mode (open and ring up)
- Indonesian language support (formal "Anda")
- Built for bakeries, cake ingredient shops, small retail

## First 10 Users

Reach out to by hand:

1. Reddit user who complained about SQL server lag (r/POS, `1opzpbo`)
2. Reddit user who wanted remote stock updates (r/POS, `1qkf3vz`)
3. Reddit user who wanted quick sell (r/POS, `1msqjcs`)
4. Reddit user with kitchen ticket issue (r/POS, `1p2e3gj`)
5. Reddit user who found it slow (r/POS, `1n4ohd3`)
6. Softwaresuggest reviewer who wanted better stock control (4★)
7. Softwaresuggest reviewer who wanted dispatch/delivery (4★)
8. Local bakery owner (personal network)
9. Small cake ingredient shop owner (personal network)
10. Local retail shop owner (personal network)

**Conversation script:** "I'm building a free POS for small shops. Saw your comment about [specific pain point]. Would you be willing to try it and give feedback? No obligation, completely free."

## Launch Day Checklist

- [ ] Build: `dotnet build Pos.sln -c Release`
- [ ] Publish: `dotnet publish src/Pos.App/Pos.App.csproj -c Release -r win-x64 --self-contained false`
- [ ] Test installer on clean Windows machine
- [ ] Verify no Aronium references remain (run sweep.py)
- [ ] Upload store listings (App Store / Play Store not applicable — Windows desktop app)
- [ ] Publish landing page
- [ ] Send email to beta waitlist
- [ ] Post to Reddit r/POS with launch announcement
- [ ] Post to Hacker News / Product Hunt
- [ ] Monitor error logs for crash reports

## Post-Launch (First 30 Days)

- [ ] Weekly check-in with first 10 users
- [ ] Fix S1/S2 bugs immediately
- [ ] Collect feature requests for next version
- [ ] Monitor review sites for new Aronium complaints
- [ ] Update landing page based on user feedback
