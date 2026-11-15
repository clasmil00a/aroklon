-- ============================================================================
-- POS Desktop - M9: Multi-tax, stock adjustment, cash drawer, PDF reprint
-- ============================================================================

-- Default settings for M9 features.
INSERT INTO settings (key, scope, scope_id, value_json, updated_at)
VALUES
  ('pos.negative_stock', 'global', '', '{"value":"block"}', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  ('pos.cash_drawer_enabled', 'global', '', '{"value":true}', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  ('pos.cash_drawer_command', 'global', '', '{"value":"ESC/POS kick drawer"}', strftime('%Y-%m-%dT%H:%M:%fZ','now'))
ON CONFLICT(key, scope, scope_id) DO NOTHING;

PRAGMA user_version = 2;
