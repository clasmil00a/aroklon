-- ============================================================================
-- POS Desktop - M10d: Should-have features with on/off toggles
-- ============================================================================

-- Toggle settings for should-have features. Default = false (off) unless noted.
INSERT INTO settings (key, scope, scope_id, value_json, updated_at)
VALUES
  -- Visual/touch sales layout (S03)
  ('pos.visual_layout_enabled', 'global', '', 'false', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  -- Per-sale tax override (S09)
  ('pos.tax_override_enabled', 'global', '', 'false', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  -- Credit payments (S20)
  ('pos.credit_payments_enabled', 'global', '', 'false', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  -- Company logo on receipt (S25)
  ('receipt.show_logo', 'global', '', 'true', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  -- Product image and color (S38)
  ('catalog.product_image_enabled', 'global', '', 'false', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  -- Age restriction warning (S40)
  ('catalog.age_restriction_enabled', 'global', '', 'false', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  -- CSV import (S41)
  ('catalog.csv_import_enabled', 'global', '', 'false', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  -- Stock history per product (S43)
  ('stock.history_enabled', 'global', '', 'false', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  -- Lock order and proforma (S54)
  ('pos.lock_order_enabled', 'global', '', 'false', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  -- Split/merge/transfer items (S55)
  ('pos.split_merge_items_enabled', 'global', '', 'false', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  -- Keyboard shortcuts (S56)
  ('pos.keyboard_shortcuts_enabled', 'global', '', 'true', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  -- Virtual keyboard (S57)
  ('pos.virtual_keyboard_enabled', 'global', '', 'false', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  -- Per-unit barcode (S63)
  ('catalog.per_unit_barcode_enabled', 'global', '', 'false', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  -- Weighing scale integration (S76)
  ('hardware.weighing_scale_enabled', 'global', '', 'false', strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  -- Multi-register shared database (S78)
  ('platform.multi_register_enabled', 'global', '', 'false', strftime('%Y-%m-%dT%H:%M:%fZ','now'))
ON CONFLICT(key, scope, scope_id) DO NOTHING;

PRAGMA user_version = 4;
