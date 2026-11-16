# Recon Map — Aroklon POS (Windows WPF, .NET 10, SQLite + Dapper)

## 1. Screen Inventory

| Screen ID | Screen Name | Route | Milestone | ViewModel | View |
|-----------|-------------|-------|-----------|-----------|------|
| S00 | Onboarding | (first run) | M0 | OnboardingViewModel | OnboardingView |
| S01 | Login | (after onboarding) | M0 | LoginViewModel | LoginView |
| S02 | Kasir (Point of Sale) | S02 | M1 | PosViewModel | PosView |
| S03 | Kasir Visual (Touch Layout) | S03 | M7 | VisualWorkspaceViewModel | VisualWorkspaceView |
| S04 | Cari Produk (F3) | S04 | M10 | ProductSearchViewModel | ProductSearchView |
| S05 | Riwayat Penjualan | S32 | M1 | SalesHistoryViewModel | SalesHistoryView |
| S06 | Pesanan Terbuka | S12 | M1 | OpenOrdersViewModel | OpenOrdersView |
| S07 | Struk | S11 | M5 | ReceiptViewModel | ReceiptView |
| S08 | Master Satuan | S42 | M2 | UnitViewModel | UnitView |
| S09 | Daftar Produk | S15 | M2 | ProductListViewModel | ProductListView |
| S10 | Daftar Harga | S39 | M2 | PriceListViewModel | PriceListView |
| S11 | Harga Produk | S41 | M2 | ProductPriceViewModel | ProductPriceView |
| S12 | Pelanggan | S10 | M2 | CustomerSelectionViewModel | CustomerSelectionView |
| S13 | Kas Masuk/Keluar | S31 | M4 | CashMovementViewModel | CashMovementView |
| S14 | Refund | S34 | M4 | RefundViewModel | RefundView |
| S15 | Tutup Hari | S35 | M4 | EndOfDayViewModel | EndOfDayView |
| S16 | Grup Produk | S43 | M8 | ProductGroupViewModel | ProductGroupView |
| S17 | Manajemen (Master Data) | management | S14 | ManagementViewModel | ManagementView |
| S18 | Pengaturan | settings | S27 | SettingsViewModel | SettingsView |
| S19 | Laporan | S44 | M8 | ReportsViewModel | ReportsView |
| S20 | Database | (internal) | S38 | DatabaseViewModel | DatabaseView |
| S21 | Manajemen Pengguna | (internal) | — | UserManagementViewModel | UserManagementView |
| S22 | Tipe Pembayaran | (internal) | S24 | PaymentTypeViewModel | PaymentTypeView |
| S23 | Editur Produk | (internal) | M2 | ProductEditorViewModel | ProductEditorView |
| S24 | Editur Satuan Harga | (internal) | M2 | ProductPriceViewModel | ProductPriceView |
| S25 | Editur Daftar Harga | (internal) | M2 | PriceListEditorViewModel | PriceListEditorView |
| S26 | Penyesuaian Stok | (internal) | M9 | StockAdjustmentViewModel | StockAdjustmentView |
| S27 | Statistik Database | (internal) | S38 | DatabaseViewModel | DatabaseView |
| S28 | Stub (placeholder) | (internal) | — | ScreenStubViewModel | StubView |

## 2. User Flows

| Flow ID | Flow Name | Screens | Description |
|---------|-----------|---------|-------------|
| F01 | Onboarding | S00 | First-run setup: shop name, admin account, database creation |
| F02 | Login | S01 | Argon2id authentication, session, lockout |
| F03 | Cash Sale | S02 → S07 | Product search, cart, checkout, stock movement, receipt |
| F04 | Touch Sale | S03 | Touch-optimized grid layout |
| F05 | Advanced Product Search | S04 | F3 search with keyboard navigation |
| F06 | Sales History | S05 | Search old transactions, reprint receipt |
| F07 | Open Orders | S06 | Resume held transactions |
| F08 | Receipt View | S07 | View, print, or save receipt |
| F09 | Unit Master | S08 | Manage selling units and conversion factors |
| F10 | Product List | S09 | List products, select for unit editing |
| F11 | Price Lists | S10 | Manage retail, wholesale, per-unit price lists |
| F12 | Product Price | S11 | View all price lists for one product |
| F13 | Customer Assignment | S12 | Select customer and default price list |
| F14 | Cash Movement | S13 | Record cash in/out |
| F15 | Refund | S14 | Refund transaction, return stock |
| F16 | End of Day | S15 | Count cash, print Z report, close register |
| F17 | Product Groups | S16 | Manage product groups for catalog |
| F18 | Product Editor | S23 | Edit product details |
| F19 | Stock Adjustment | S26 | Manual stock adjustment |
| F20 | Reports | S19 | Sales, stock, payment, customer, tax reports |
| F21 | Settings | S18 | Theme, cashier behavior, printing, database |
| F22 | User Management | S21 | Admin user accounts |
| F23 | Payment Types | S22 | Configure payment methods |
| F24 | Database Check | S20 / S27 | Integrity check, backup |
| F25 | Home Navigation | (shell) | Main menu navigation between screens |

## 3. Component List

- **Views**: 33 XAML views in `src/Pos.App/Views/`
- **ViewModels**: 33 ViewModels in `src/Pos.App/ViewModels/`
- **Services**: AuthService, SaleService, ProductService, UnitService, PriceListService, CustomerService, CashMovementService, RefundService, EndOfDayService, TaxService, StockAdjustmentService, CashDrawerService, CreditPaymentService, StockHistoryService, OrderLockService, CsvImportService, VirtualKeyboardService, WeighingScaleService, MultiRegisterService, ReportsService, BackupService, ThemeService, UserPreferencesService
- **Domain Models**: Product, Unit, PriceList, Customer, Sale, CartLine, Payment, TaxRate, CompanyInfo, UserSession
- **Migrations**: 4 SQL migration files (0001_initial through 0004_should_have_toggles)

## 4. Inferred Data Model

| Entity | Key Fields |
|--------|-----------|
| Product | Id, Code, Name, GroupId, UnitId, BasePrice, IsActive |
| Unit | Id, Code, Name, FactorToBaseMilli |
| PriceList | Id, Name, IsActive |
| ProductPrice | ProductId, PriceListId, Price, FactorToBaseMilli |
| Customer | Id, Name, Phone, DefaultPriceListId |
| Sale | Id, TransactionNo, DateTime, Total, CustomerId, PaymentTypeId, Status |
| SaleLine | SaleId, ProductId, UnitId, Qty, Price, Tax |
| Payment | Id, SaleId, Type, Amount, DateTime |
| StockMovement | Id, ProductId, UnitId, Qty, Type, DateTime, Reason |
| CashMovement | Id, Type, Amount, DateTime, Note |
| EndOfDay | Id, Date, CashCount, CardCount, Total, Status |
| TaxRate | Id, Name, Rate, IsActive |
| UserSession | Id, Username, Role, LoginTime |
| Setting | Key, Value |
| Toggle | FeatureId, IsEnabled |

## 5. Feature Matrix

| Feature | ID | Milestone | Status | Notes |
|---------|-----|-----------|--------|-------|
| Onboarding | S00 | M0 | yes | Shop name, admin account |
| Login | S01 | M0 | yes | Argon2id, lockout 5/15min |
| Cash Sale | S02 | M1 | yes | Product search, cart, checkout |
| Touch Sale | S03 | M7 | yes | Grid layout |
| Product Search (F3) | S04 | M10 | yes | Advanced search, keyboard nav |
| Sales History | S32 | M1 | yes | Search, reprint |
| Open Orders | S12 | M1 | yes | Resume held |
| Receipt | S11 | M5 | yes | View, print, save |
| Unit Master | S42 | M2 | yes | Conversion factors |
| Product List | S15 | M2 | yes | List products |
| Price Lists | S39 | M2 | yes | Retail, wholesale |
| Product Price | S41 | M2 | yes | Per product price lists |
| Customer | S10 | M2 | yes | Default price list |
| Cash Movement | S31 | M4 | yes | In/out |
| Refund | S34 | M4 | yes | Return stock |
| End of Day | S35 | M4 | yes | Z report |
| Product Groups | S43 | M8 | yes | Catalog groups |
| Product Editor | S23 | M2 | yes | Edit products |
| Stock Adjustment | S26 | M9 | yes | Manual adjustment |
| Reports | S44 | M8 | yes | CSV/TXT export |
| Settings | S27 | — | yes | Theme, behavior |
| User Management | — | — | yes | Admin accounts |
| Payment Types | S24 | — | yes | Configure methods |
| Database Check | S38 | — | yes | Integrity, backup |
| Credit Payments | — | M10d | toggle | Should-have |
| Company Logo | — | M10d | toggle | Should-have |
| Product Image/Color | — | M10d | toggle | Should-have |
| Age Restriction | — | M10d | toggle | Should-have |
| CSV Import | — | M10d | toggle | Should-have |
| Stock History | — | M10d | toggle | Should-have |
| Lock Order | — | M10d | toggle | Should-have |
| Split/Merge Items | — | M10d | toggle | Should-have |
| Keyboard Shortcuts | — | M10d | toggle | Should-have |
| Virtual Keyboard | — | M10d | toggle | Should-have |
| Per-Unit Barcode | — | M10d | toggle | Should-have |
| Weighing Scale | — | M10d | toggle | Should-have |
| Multi-Register | — | M10d | toggle | Should-have |
