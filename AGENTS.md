# Aroklon — agent notes

## Product conventions

- **Every "Reason" / "Alasan" field is optional.** Never mark a reason input as
  `required`, and never reject a save because the reason is empty. Store an empty
  string when the user leaves it blank. This applies to stock adjustments, cash
  movements, document deletion, refunds, and any future feature that asks for a
  reason.
- Fokus pengembangan ada di branch `tes`. Jangan mengubah branch `main`.
- Build/patch application: `npm run package` (Windows portable + app asar + latest.json).
- The app is Indonesian-first; user-facing strings should have Indonesian
  translations in `src/renderer/i18n.js` where practical.