using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Services;
using Pos.Domain;
using Xunit;

namespace Pos.Data.Tests;

public sealed class AroniumImportTests
{
    [Fact]
    public async Task Preview_reads_aronium_master_data_and_warns_about_unsupported_parts()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();
        await using var fixture = await AroniumFixture.CreateAsync();

        var preview = await db.AroniumImport.InspectAsync(fixture.DatabasePath);

        Assert.True(preview.CanImport);
        Assert.Equal(4, preview.ProductCount);
        Assert.Equal(2, preview.GroupCount);
        Assert.Equal(3, preview.BarcodeCount);
        Assert.Equal(3, preview.TaxCount);
        Assert.Equal(1, preview.CustomerCount);
        Assert.Equal(1, preview.SupplierCount);
        Assert.Equal(2, preview.PaymentTypeCount);
        Assert.Equal(3, preview.StockProductCount);
        Assert.Equal(1, preview.DocumentCount);
        Assert.Contains(preview.Warnings, warning => warning.Contains("Riwayat transaksi"));
        Assert.Contains(preview.Warnings, warning => warning.Contains("Pajak tetap"));
        Assert.Contains(preview.Warnings, warning => warning.Contains("Diskon khusus pelanggan"));
        Assert.Contains(preview.Warnings, warning => warning.Contains("Foto produk"));
    }

    [Fact]
    public async Task Import_merges_catalog_customers_payment_types_and_opening_stock()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();
        await using var fixture = await AroniumFixture.CreateAsync();

        var result = await db.AroniumImport.ImportAsync(new AroniumImportOptions(
            fixture.DatabasePath,
            ImportCompany: true,
            ImportCustomers: true,
            ImportPaymentTypes: true,
            ImportStock: true,
            UpdateExisting: true));

        Assert.Equal(4, result.ProductsCreated);
        Assert.Equal(0, result.ProductsUpdated);
        Assert.Equal(2, result.GroupsCreated);
        Assert.Equal(3, result.BarcodesCreated);
        Assert.Equal(1, result.TaxesCreated);
        Assert.Equal(2, result.CustomersCreated);
        Assert.Equal(1, result.PaymentTypesCreated);
        Assert.Equal(1, result.PaymentTypesUpdated);
        Assert.Equal(2, result.StockMovementsCreated);
        Assert.True(result.CompanyUpdated);

        await using var connection = await db.OpenAsync();

        var kopi = await connection.QuerySingleAsync<KopiRow>(
            """
            SELECT
              p.id, p.name, p.code, p.price_minor, p.cost_minor, p.markup_bp,
              p.price_includes_tax, p.price_change_allowed, p.active,
              g.name AS group_name,
              pu.id AS base_unit_id
            FROM products p
            JOIN product_groups g ON g.id = p.group_id
            JOIN product_units pu ON pu.product_id = p.id AND pu.is_base = 1
            WHERE p.code = 'KOP-001';
            """);
        Assert.Equal("Kopi Susu", kopi.Name);
        Assert.Equal(15_000, kopi.PriceMinor);
        Assert.Equal(8_000, kopi.CostMinor);
        Assert.Equal(8_750, kopi.MarkupBp);
        Assert.Equal(1, kopi.PriceIncludesTax);
        Assert.Equal(0, kopi.PriceChangeAllowed);
        Assert.Equal("Kopi", kopi.GroupName);

        var barcodes = (await connection.QueryAsync<string>(
            "SELECT barcode FROM product_barcodes WHERE product_unit_id = @unitId ORDER BY barcode;",
            new { unitId = kopi.BaseUnitId })).ToList();
        Assert.Equal(["111", "222"], barcodes);

        var taxes = (await connection.QueryAsync<string>(
            """
            SELECT t.name
            FROM product_taxes pt
            JOIN tax_rates t ON t.id = pt.tax_rate_id
            JOIN products p ON p.id = pt.product_id
            WHERE p.code = 'KOP-001';
            """)).ToList();
        Assert.Contains("PPN 11%", taxes);

        var teh = await connection.QuerySingleAsync<TehRow>(
            """
            SELECT p.id, p.price_minor, b.barcode
            FROM products p
            JOIN product_units pu ON pu.product_id = p.id AND pu.is_base = 1
            JOIN product_barcodes b ON b.product_unit_id = pu.id
            WHERE p.name = 'Teh Botol';
            """);
        Assert.Equal(5_000, teh.PriceMinor);
        Assert.Equal("899123", teh.Barcode);

        var service = await connection.QuerySingleAsync<ProductFlagsRow>(
            "SELECT is_service, active FROM products WHERE code = 'JASA-1';");
        Assert.Equal(1, service.IsService);

        var old = await connection.QuerySingleAsync<ProductFlagsRow>(
            "SELECT is_service, active FROM products WHERE code = 'OLD-1';");
        Assert.Equal(0, old.Active);

        var stock = (await connection.QueryAsync<StockRow>(
            """
            SELECT p.name, SUM(sm.qty_delta_base_milli) AS qty
            FROM stock_movements sm
            JOIN products p ON p.id = sm.product_id
            GROUP BY p.id, p.name
            ORDER BY p.name;
            """)).ToList();
        Assert.Equal(2, stock.Count);
        Assert.Contains(stock, row => row.Name == "Kopi Susu" && row.Qty == 12_500);
        Assert.Contains(stock, row => row.Name == "Teh Botol" && row.Qty == 3_000);
        Assert.DoesNotContain(stock, row => row.Name == "Jasa Antar");

        var customers = (await connection.QueryAsync<CustomerRow>(
            "SELECT name, type, address FROM customers ORDER BY name;")).ToList();
        Assert.Equal(2, customers.Count);
        Assert.Contains(customers, row => row.Name == "Budi" && row.Type == "customer");
        var supplier = Assert.Single(customers, row => row.Name == "CV Sumber");
        Assert.Equal("supplier", supplier.Type);
        Assert.Contains("Bandung", supplier.Address);

        var payment = await connection.QuerySingleAsync<PaymentRow>(
            "SELECT is_default FROM payment_types WHERE name = 'Transfer Bank';");
        Assert.Equal(0, payment.IsDefault);

        var company = await connection.QuerySingleAsync<string>(
            "SELECT name FROM company WHERE id = 'company';");
        Assert.Equal("Toko Aronium", company);

        var auditCount = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM audit_events WHERE action = 'aronium.imported';");
        Assert.Equal(1, auditCount);
    }

    [Fact]
    public async Task Import_can_be_repeated_without_duplicates()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();
        await using var fixture = await AroniumFixture.CreateAsync();
        var options = new AroniumImportOptions(
            fixture.DatabasePath,
            ImportCompany: true,
            ImportCustomers: true,
            ImportPaymentTypes: true,
            ImportStock: true,
            UpdateExisting: true);

        await db.AroniumImport.ImportAsync(options);
        var second = await db.AroniumImport.ImportAsync(options);

        Assert.Equal(0, second.ProductsCreated);
        Assert.Equal(4, second.ProductsUpdated);
        Assert.Equal(0, second.GroupsCreated);
        Assert.Equal(0, second.BarcodesCreated);
        Assert.Equal(0, second.TaxesCreated);
        Assert.Equal(0, second.CustomersCreated);
        Assert.Equal(2, second.CustomersUpdated);
        Assert.Equal(0, second.PaymentTypesCreated);
        Assert.Equal(2, second.PaymentTypesUpdated);
        Assert.Equal(0, second.StockMovementsCreated);
        Assert.Equal(2, second.StockProductsSkipped);

        await using var connection = await db.OpenAsync();
        Assert.Equal(4, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM products;"));
        Assert.Equal(2, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM product_groups;"));
        Assert.Equal(3, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM product_barcodes;"));
        Assert.Equal(2, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM customers;"));
        Assert.Equal(3, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM tax_rates;"));
        Assert.Equal(2, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM stock_movements;"));
    }

    [Fact]
    public async Task Existing_product_can_be_skipped()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();
        await using var fixture = await AroniumFixture.CreateAsync();

        await db.AroniumImport.ImportAsync(new AroniumImportOptions(
            fixture.DatabasePath,
            ImportCompany: false,
            ImportCustomers: false,
            ImportPaymentTypes: false,
            ImportStock: false,
            UpdateExisting: true));

        var second = await db.AroniumImport.ImportAsync(new AroniumImportOptions(
            fixture.DatabasePath,
            ImportCompany: false,
            ImportCustomers: false,
            ImportPaymentTypes: false,
            ImportStock: false,
            UpdateExisting: false));

        Assert.Equal(0, second.ProductsCreated);
        Assert.Equal(0, second.ProductsUpdated);
        Assert.Equal(4, second.ProductsSkipped);
        Assert.Equal(0, second.CustomersCreated);
        Assert.Equal(0, second.CustomersUpdated);
        Assert.Equal(0, second.PaymentTypesCreated);
        Assert.Equal(0, second.PaymentTypesUpdated);
    }

    [Fact]
    public async Task Sql_server_backup_is_rejected_with_migration_guidance()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();
        var path = Path.Combine(db.RootPath, "aronium-pro.bak");
        await File.WriteAllTextAsync(path, "TAPE\u0000Microsoft SQL Server backup");

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => db.AroniumImport.InspectAsync(path));

        Assert.Contains("Aronium Pro", exception.Message);
        Assert.Contains("CSV", exception.Message);
    }

    [Fact]
    public async Task Non_aronium_sqlite_file_is_rejected()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();
        var path = Path.Combine(db.RootPath, "not-aronium.db");
        await using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Foo (Id INTEGER PRIMARY KEY, Name TEXT);";
            await command.ExecuteNonQueryAsync();
        }

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => db.AroniumImport.InspectAsync(path));

        Assert.Contains("bukan database Aronium Lite", exception.Message);
    }

    private sealed class KopiRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public long PriceMinor { get; set; }
        public long CostMinor { get; set; }
        public int MarkupBp { get; set; }
        public int PriceIncludesTax { get; set; }
        public int PriceChangeAllowed { get; set; }
        public int Active { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string BaseUnitId { get; set; } = string.Empty;
    }

    private sealed class TehRow
    {
        public string Id { get; set; } = string.Empty;
        public long PriceMinor { get; set; }
        public string Barcode { get; set; } = string.Empty;
    }

    private sealed class ProductFlagsRow
    {
        public int IsService { get; set; }
        public int Active { get; set; }
    }

    private sealed class StockRow
    {
        public string Name { get; set; } = string.Empty;
        public long Qty { get; set; }
    }

    private sealed class CustomerRow
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Address { get; set; }
    }

    private sealed class PaymentRow
    {
        public int IsDefault { get; set; }
    }

    private sealed class AroniumFixture : IAsyncDisposable
    {
        private AroniumFixture(string rootPath, string databasePath)
        {
            RootPath = rootPath;
            DatabasePath = databasePath;
        }

        public string RootPath { get; }

        public string DatabasePath { get; }

        public static async Task<AroniumFixture> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "pos-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "pos.db");
            var fixture = new AroniumFixture(root, path);

            await using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = Schema;
            await command.ExecuteNonQueryAsync();

            return fixture;
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup for a temp source database.
            }

            return ValueTask.CompletedTask;
        }

        private const string Schema = """
            CREATE TABLE ProductGroup (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              Name TEXT NOT NULL,
              ParentGroupId INTEGER NULL,
              Rank INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE Product (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              ProductGroupId INTEGER NULL,
              Name TEXT NOT NULL,
              Code TEXT NULL,
              PLU INTEGER NULL,
              MeasurementUnit TEXT NULL,
              Price DECIMAL NOT NULL DEFAULT 0,
              IsTaxInclusivePrice INTEGER NOT NULL DEFAULT 1,
              IsPriceChangeAllowed INTEGER NOT NULL DEFAULT 0,
              IsService INTEGER NOT NULL DEFAULT 0,
              IsUsingDefaultQuantity INTEGER NOT NULL DEFAULT 1,
              IsEnabled INTEGER NOT NULL DEFAULT 1,
              Description TEXT NULL,
              Cost DECIMAL NOT NULL DEFAULT 0,
              Markup DECIMAL NOT NULL DEFAULT 0,
              Image BLOB NULL,
              Color TEXT NOT NULL DEFAULT 'Transparent',
              AgeRestriction DECIMAL NULL
            );

            CREATE TABLE Barcode (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              ProductId INTEGER NOT NULL,
              Value TEXT NOT NULL
            );

            CREATE TABLE Tax (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              Name TEXT NOT NULL,
              Rate DECIMAL NOT NULL DEFAULT 0,
              IsFixed INTEGER NOT NULL DEFAULT 0,
              IsEnabled INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE ProductTax (
              ProductId INTEGER NOT NULL,
              TaxId INTEGER NOT NULL,
              PRIMARY KEY (ProductId, TaxId)
            );

            CREATE TABLE Customer (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              Code TEXT NULL,
              Name TEXT NOT NULL,
              TaxNumber TEXT NULL,
              Address TEXT NULL,
              PostalCode TEXT NULL,
              City TEXT NULL,
              Email TEXT NULL,
              PhoneNumber TEXT NULL,
              IsEnabled INTEGER NOT NULL DEFAULT 1,
              IsCustomer INTEGER NOT NULL DEFAULT 1,
              IsSupplier INTEGER NOT NULL DEFAULT 1,
              StreetName TEXT NULL,
              BuildingNumber TEXT NULL,
              CitySubdivisionName TEXT NULL,
              CountrySubentity TEXT NULL
            );

            CREATE TABLE PaymentType (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              Name TEXT NOT NULL,
              ShortcutKey TEXT NULL,
              IsEnabled INTEGER NOT NULL DEFAULT 1,
              OpenCashDrawer INTEGER NOT NULL DEFAULT 1,
              MarkAsPaid INTEGER NOT NULL DEFAULT 1,
              Ordinal INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE Stock (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              ProductId INTEGER NOT NULL,
              WarehouseId INTEGER NOT NULL,
              Quantity DECIMAL NOT NULL
            );

            CREATE TABLE Company (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              Name TEXT NOT NULL,
              Address TEXT NULL,
              PostalCode TEXT NULL,
              City TEXT NULL,
              TaxNumber TEXT NULL,
              Email TEXT NULL,
              PhoneNumber TEXT NULL
            );

            CREATE TABLE Document (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              Number TEXT NOT NULL
            );

            CREATE TABLE CustomerDiscount (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              CustomerId INTEGER NOT NULL,
              Type INTEGER NOT NULL DEFAULT 0,
              Value DECIMAL NOT NULL DEFAULT 0
            );

            INSERT INTO ProductGroup (Id, Name, ParentGroupId, Rank) VALUES
              (1, 'Minuman', NULL, 2),
              (2, 'Kopi', 1, 1);

            INSERT INTO Product
              (Id, ProductGroupId, Name, Code, MeasurementUnit, Price, Cost, Markup,
               IsTaxInclusivePrice, IsPriceChangeAllowed, IsService, IsEnabled,
               Description, Color, Image)
            VALUES
              (1, 2, 'Kopi Susu', 'KOP-001', 'pcs', 15000, 8000, 87.5,
               1, 0, 0, 1, 'Kopi susu gula aren', 'Transparent', NULL),
              (2, 1, 'Teh Botol', NULL, 'pcs', 5000, 3000, 66.67,
               0, 1, 0, 1, NULL, 'Transparent', NULL),
              (3, NULL, 'Jasa Antar', 'JASA-1', NULL, 10000, 0, 0,
               1, 1, 1, 1, NULL, 'Transparent', X'01'),
              (4, NULL, 'Produk Lama', 'OLD-1', 'pcs', 1000, 500, 100,
               1, 0, 0, 0, NULL, 'Transparent', NULL);

            INSERT INTO Barcode (ProductId, Value) VALUES
              (1, '111'),
              (1, '222'),
              (2, '899123');

            INSERT INTO Tax (Id, Name, Rate, IsFixed, IsEnabled) VALUES
              (1, 'PPN 11%', 11, 0, 1),
              (2, 'Pajak Tetap', 1000, 1, 1),
              (3, 'Service 5%', 5, 0, 1);

            INSERT INTO ProductTax (ProductId, TaxId) VALUES
              (1, 1),
              (2, 1),
              (3, 3),
              (4, 2);

            INSERT INTO Customer
              (Id, Code, Name, TaxNumber, Address, PostalCode, City, Email, PhoneNumber,
               IsEnabled, IsCustomer, IsSupplier, StreetName, BuildingNumber)
            VALUES
              (1, 'C-001', 'Budi', '01.234', 'Jl. Mawar', '12345', 'Jakarta',
               'budi@example.id', '0812', 1, 1, 0, NULL, NULL),
              (2, NULL, 'CV Sumber', NULL, NULL, '40123', 'Bandung', NULL, NULL,
               1, 0, 1, 'Jl. Industri', '5');

            INSERT INTO PaymentType
              (Id, Name, ShortcutKey, IsEnabled, OpenCashDrawer, MarkAsPaid, Ordinal)
            VALUES
              (1, 'Cash', 'F1', 1, 1, 1, 1),
              (2, 'Transfer Bank', NULL, 1, 0, 1, 2);

            INSERT INTO Stock (ProductId, WarehouseId, Quantity) VALUES
              (1, 1, 12.5),
              (2, 1, 3),
              (4, 1, 0);

            INSERT INTO Company
              (Id, Name, Address, PostalCode, City, TaxNumber, Email, PhoneNumber)
            VALUES
              (1, 'Toko Aronium', 'Jl. Merdeka 10', '10110', 'Jakarta',
               '02.345', 'toko@aronium.id', '021-555');

            INSERT INTO Document (Number) VALUES ('INV-1');

            INSERT INTO CustomerDiscount (CustomerId, Type, Value) VALUES (1, 0, 5);
            """;
    }
}
