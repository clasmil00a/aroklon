-- ============================================================================
-- POS Desktop - M9b: Tax on/off toggle
-- ============================================================================

-- Global toggle for tax. When off, all tax computation returns 0 and tax is
-- hidden on receipts.
INSERT INTO settings (key, scope, scope_id, value_json, updated_at)
VALUES
  ('pos.tax_enabled', 'global', '', 'true', strftime('%Y-%m-%dT%H:%M:%fZ','now'))
ON CONFLICT(key, scope, scope_id) DO NOTHING;

PRAGMA user_version = 3;
