using System.Globalization;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Pos.Domain.Time;

namespace Pos.Data.Services;

/// <summary>
/// Imports master data from an Aronium Lite <c>pos.db</c> SQLite database.
/// The source is opened read-only and the target is updated in one transaction.
/// Existing rows are matched by code/barcode/name so an import can be repeated
/// safely without creating duplicates.
/// </summary>
public sealed class AroniumImportService
{
    private static readonly string[] KnownTables =
    [
        "Product",
        "ProductGroup",
        "Barcode",
        "Tax",
        "ProductTax",
        "Customer",
        "PaymentType",
        "Stock",
        "Company",
        "Document",
        "DocumentItem",
        "CustomerDiscount"
    ];

    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public AroniumImportService(
        IDbConnectionFactory factory,
        ISessionStore sessions,
        IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    /// <summary>
    /// Reads an Aronium database without changing either side and returns a
    /// preview of what can be imported.
    /// </summary>
    public async Task<AroniumImportPreview> InspectAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        var snapshot = await ReadSnapshotAsync(sourcePath, cancellationToken).ConfigureAwait(false);

        return new AroniumImportPreview(
            FilePath: snapshot.FilePath,
            FileName: snapshot.FileName,
            FileSizeBytes: snapshot.FileSizeBytes,
            ProductCount: snapshot.Products.Count,
            GroupCount: snapshot.Groups.Count,
            BarcodeCount: snapshot.Barcodes.Count,
            TaxCount: snapshot.Taxes.Count,
            CustomerCount: snapshot.Customers.Count(c => IsCustomer(c)),
            SupplierCount: snapshot.Customers.Count(c => IsSupplier(c)),
            PaymentTypeCount: snapshot.PaymentTypes.Count,
            StockProductCount: snapshot.Stocks
                .Select(stock => stock.ProductId)
                .Distinct()
                .Count(),
            DocumentCount: snapshot.DocumentCount,
            Warnings: snapshot.Warnings);
    }

    /// <summary>
    /// Merges Aronium master data into the live database. The whole import is
    /// atomic: a failure rolls back every product, customer, and stock row.
    /// </summary>
    public async Task<AroniumImportResult> ImportAsync(
        AroniumImportOptions options,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        if (!session.Can(PermissionKeys.SettingsManage))
        {
            throw new PermissionException(
                "Anda tidak memiliki izin untuk mengimpor data Aronium.");
        }

        var snapshot = await ReadSnapshotAsync(options.FilePath, cancellationToken)
            .ConfigureAwait(false);
        if (snapshot.Products.Count == 0)
        {
            throw new ValidationException(
                "aronium.no_products",
                "Database Aronium ini tidak berisi produk. Tidak ada data yang diimpor.");
        }

        var now = _clock.UtcNow;
        var warnings = new WarningCollector(snapshot.Warnings);
        var counters = new ImportCounters();

        await using var connection = await _factory.OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var currencyMinorUnit = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT currency_minor_unit FROM company WHERE id = 'company';",
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            var importedCompanyName = options.ImportCompany
                ? await ImportCompanyAsync(
                    connection,
                    transaction,
                    snapshot,
                    now,
                    warnings,
                    cancellationToken).ConfigureAwait(false)
                : null;

            var groups = await ImportGroupsAsync(
                connection,
                transaction,
                snapshot,
                now,
                counters,
                cancellationToken).ConfigureAwait(false);

            var units = await LoadUnitsAsync(connection, transaction, cancellationToken)
                .ConfigureAwait(false);

            var taxes = await ImportTaxesAsync(
                connection,
                transaction,
                snapshot,
                now,
                counters,
                warnings,
                cancellationToken).ConfigureAwait(false);

            var products = await ImportProductsAsync(
                connection,
                transaction,
                snapshot,
                groups,
                units,
                taxes,
                options,
                currencyMinorUnit,
                now,
                counters,
                warnings,
                cancellationToken).ConfigureAwait(false);

            if (options.ImportCustomers)
            {
                await ImportCustomersAsync(
                    connection,
                    transaction,
                    snapshot,
                    options,
                    now,
                    counters,
                    warnings,
                    cancellationToken).ConfigureAwait(false);
            }

            if (options.ImportPaymentTypes)
            {
                await ImportPaymentTypesAsync(
                    connection,
                    transaction,
                    snapshot,
                    options,
                    now,
                    counters,
                    warnings,
                    cancellationToken).ConfigureAwait(false);
            }

            if (options.ImportStock)
            {
                await ImportStockAsync(
                    connection,
                    transaction,
                    snapshot,
                    products,
                    session.UserId,
                    currencyMinorUnit,
                    now,
                    counters,
                    warnings,
                    cancellationToken).ConfigureAwait(false);
            }

            await AuditWriter.WriteAsync(
                connection,
                transaction,
                session.UserId,
                "aronium.imported",
                "database",
                null,
                new
                {
                    source = snapshot.FileName,
                    productsCreated = counters.ProductsCreated,
                    productsUpdated = counters.ProductsUpdated,
                    customersCreated = counters.CustomersCreated,
                    stockMovementsCreated = counters.StockMovementsCreated
                },
                now,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new AroniumImportResult(
                ProductsCreated: counters.ProductsCreated,
                ProductsUpdated: counters.ProductsUpdated,
                ProductsSkipped: counters.ProductsSkipped,
                GroupsCreated: counters.GroupsCreated,
                UnitsCreated: counters.UnitsCreated,
                BarcodesCreated: counters.BarcodesCreated,
                TaxesCreated: counters.TaxesCreated,
                CustomersCreated: counters.CustomersCreated,
                CustomersUpdated: counters.CustomersUpdated,
                CustomersSkipped: counters.CustomersSkipped,
                PaymentTypesCreated: counters.PaymentTypesCreated,
                PaymentTypesUpdated: counters.PaymentTypesUpdated,
                StockMovementsCreated: counters.StockMovementsCreated,
                StockProductsSkipped: counters.StockProductsSkipped,
                CompanyUpdated: importedCompanyName is not null,
                CompanyName: importedCompanyName,
                Warnings: warnings.Build());
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<SourceSnapshot> ReadSnapshotAsync(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        var fullPath = ValidateSourcePath(sourcePath);
        var fileInfo = new FileInfo(fullPath);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
            DefaultTimeout = 10
        }.ToString();

        await using var source = new SqliteConnection(connectionString);
        try
        {
            await source.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            throw new ValidationException(
                "aronium.source_open",
                "Database Aronium tidak dapat dibuka. Tutup aplikasi Aronium terlebih dahulu, " +
                "lalu pilih file pos.db dari folder Aronium\\Data. (" + exception.Message + ")");
        }

        var tables = (await source.QueryAsync<string>(new CommandDefinition(
                "SELECT name FROM sqlite_master WHERE type = 'table';",
                cancellationToken: cancellationToken)).ConfigureAwait(false))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!tables.Contains("Product"))
        {
            throw new ValidationException(
                "aronium.not_lite",
                "File ini bukan database Aronium Lite: tabel Product tidak ditemukan. " +
                "Pastikan Anda memilih pos.db dari folder Aronium\\Data.");
        }

        var aroniumMarkers = new[] { "ProductGroup", "Barcode", "Tax", "PaymentType", "Document" }
            .Count(tables.Contains);
        if (aroniumMarkers < 2)
        {
            throw new ValidationException(
                "aronium.not_lite",
                "Struktur database tidak dikenali sebagai Aronium Lite. " +
                "Pilih file pos.db yang dibuat oleh aplikasi Aronium.");
        }

        var snapshot = new SourceSnapshot(fullPath, fileInfo.Name, fileInfo.Length);
        var warnings = new WarningCollector();

        var productColumns = await ReadColumnsAsync(source, "Product", cancellationToken)
            .ConfigureAwait(false);
        RequireColumns("Product", productColumns, "Id", "Name");
        snapshot.Products = (await source.QueryAsync<SourceProduct>(
                new CommandDefinition(
                    $"""
                    SELECT
                      "Id" AS "Id",
                      "Name" AS "Name",
                      {Column(productColumns, "Code", "Code")},
                      {Column(productColumns, "ProductGroupId", "ProductGroupId")},
                      {Column(productColumns, "MeasurementUnit", "MeasurementUnit")},
                      {Column(productColumns, "Price", "Price")},
                      {Column(productColumns, "Cost", "Cost")},
                      {Column(productColumns, "Markup", "Markup")},
                      {Column(productColumns, "AgeRestriction", "AgeRestriction")},
                      {Column(productColumns, "IsTaxInclusivePrice", "IsTaxInclusivePrice")},
                      {Column(productColumns, "IsPriceChangeAllowed", "IsPriceChangeAllowed")},
                      {Column(productColumns, "IsService", "IsService")},
                      {Column(productColumns, "IsUsingDefaultQuantity", "IsUsingDefaultQuantity")},
                      {Column(productColumns, "IsEnabled", "IsEnabled")},
                      {Column(productColumns, "Description", "Description")},
                      {Column(productColumns, "Color", "Color")},
                      {Column(productColumns, "PLU", "PLU")}
                    FROM "Product";
                    """,
                    cancellationToken: cancellationToken)).ConfigureAwait(false))
            .ToList();

        snapshot.Groups = await ReadOptionalTableAsync<SourceProductGroup>(
            source,
            tables,
            "ProductGroup",
            """
            SELECT
              "Id" AS "Id",
              "Name" AS "Name",
              {0},
              {1}
            FROM "ProductGroup";
            """,
            ["ParentGroupId", "Rank"],
            cancellationToken).ConfigureAwait(false);

        snapshot.Barcodes = await ReadOptionalTableAsync<SourceBarcode>(
            source,
            tables,
            "Barcode",
            """
            SELECT
              "Id" AS "Id",
              "ProductId" AS "ProductId",
              {0}
            FROM "Barcode";
            """,
            ["Value"],
            cancellationToken).ConfigureAwait(false);

        snapshot.Taxes = await ReadOptionalTableAsync<SourceTax>(
            source,
            tables,
            "Tax",
            """
            SELECT
              "Id" AS "Id",
              "Name" AS "Name",
              {0},
              {1},
              {2}
            FROM "Tax";
            """,
            ["Rate", "IsEnabled", "IsFixed"],
            cancellationToken).ConfigureAwait(false);

        snapshot.ProductTaxes = await ReadOptionalTableAsync<SourceProductTax>(
            source,
            tables,
            "ProductTax",
            """
            SELECT
              "ProductId" AS "ProductId",
              "TaxId" AS "TaxId"
            FROM "ProductTax";
            """,
            [],
            cancellationToken).ConfigureAwait(false);

        snapshot.Customers = await ReadOptionalTableAsync<SourceCustomer>(
            source,
            tables,
            "Customer",
            """
            SELECT
              "Id" AS "Id",
              {0},
              "Name" AS "Name",
              {1},
              {2},
              {3},
              {4},
              {5},
              {6},
              {7},
              {8},
              {9},
              {10},
              {11},
              {12},
              {13}
            FROM "Customer";
            """,
            [
                "Code", "TaxNumber", "Address", "PostalCode", "City", "Email",
                "PhoneNumber", "IsEnabled", "IsCustomer", "IsSupplier",
                "StreetName", "BuildingNumber", "CitySubdivisionName", "CountrySubentity"
            ],
            cancellationToken).ConfigureAwait(false);

        snapshot.PaymentTypes = await ReadOptionalTableAsync<SourcePaymentType>(
            source,
            tables,
            "PaymentType",
            """
            SELECT
              "Id" AS "Id",
              "Name" AS "Name",
              {0},
              {1},
              {2},
              {3},
              {4}
            FROM "PaymentType";
            """,
            ["ShortcutKey", "IsEnabled", "OpenCashDrawer", "MarkAsPaid", "Ordinal"],
            cancellationToken).ConfigureAwait(false);

        snapshot.Stocks = await ReadOptionalTableAsync<SourceStock>(
            source,
            tables,
            "Stock",
            """
            SELECT
              "ProductId" AS "ProductId",
              {0}
            FROM "Stock";
            """,
            ["Quantity"],
            cancellationToken).ConfigureAwait(false);

        snapshot.Companies = await ReadOptionalTableAsync<SourceCompany>(
            source,
            tables,
            "Company",
            """
            SELECT
              "Id" AS "Id",
              "Name" AS "Name",
              {0},
              {1},
              {2},
              {3},
              {4},
              {5}
            FROM "Company";
            """,
            ["Address", "PostalCode", "City", "TaxNumber", "Email", "PhoneNumber"],
            cancellationToken).ConfigureAwait(false);

        snapshot.DocumentCount = await CountRowsAsync(source, tables, "Document", cancellationToken)
            .ConfigureAwait(false);
        snapshot.CustomerDiscountCount = await CountRowsAsync(
            source,
            tables,
            "CustomerDiscount",
            cancellationToken).ConfigureAwait(false);

        if (productColumns.Contains("image"))
        {
            snapshot.ProductImageCount = await source.ExecuteScalarAsync<long>(new CommandDefinition(
                "SELECT COUNT(*) FROM \"Product\" WHERE \"Image\" IS NOT NULL;",
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        if (snapshot.Products.Count == 0)
        {
            warnings.Add("Database Aronium tidak berisi produk.");
        }

        if (snapshot.DocumentCount > 0)
        {
            warnings.Add(
                $"Riwayat transaksi Aronium terdeteksi ({snapshot.DocumentCount:N0} dokumen). " +
                "Yang diimpor hanya data master, pelanggan, dan stok awal; riwayat transaksi tidak diubah.");
        }

        if (snapshot.Taxes.Any(tax => AsLong(tax.IsFixed) == 1))
        {
            warnings.Add("Pajak tetap (fixed amount) dari Aronium dilewati karena hanya pajak persentase yang didukung.");
        }

        if (snapshot.CustomerDiscountCount > 0)
        {
            warnings.Add(
                $"Diskon khusus pelanggan ({snapshot.CustomerDiscountCount:N0} aturan) belum diimpor. " +
                "Atur ulang diskon pelanggan dari menu Pelanggan bila diperlukan.");
        }

        if (snapshot.ProductImageCount > 0)
        {
            warnings.Add(
                $"Foto produk ({snapshot.ProductImageCount:N0}) tidak diimpor. " +
                "Tambahkan foto dari editor produk bila diperlukan.");
        }

        if (!tables.Contains("Stock"))
        {
            warnings.Add("Tabel stok Aronium tidak ditemukan; stok awal tidak tersedia.");
        }
        else if (snapshot.Stocks.Count > 0)
        {
            warnings.Add("Stok awal hanya dimasukkan untuk produk yang belum memiliki pergerakan stok di aplikasi ini.");
        }

        snapshot.Warnings = warnings.Build();
        return snapshot;
    }

    private async Task<string?> ImportCompanyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SourceSnapshot snapshot,
        DateTimeOffset now,
        WarningCollector warnings,
        CancellationToken cancellationToken)
    {
        var source = snapshot.Companies.FirstOrDefault(company =>
            !string.IsNullOrWhiteSpace(company.Name));
        if (source is null)
        {
            return null;
        }

        var current = await connection.QuerySingleOrDefaultAsync<TargetCompany>(
            new CommandDefinition(
                """
                SELECT name, legal_name, tax_number, address, phone, email
                FROM company WHERE id = 'company';
                """,
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (current is null)
        {
            return null;
        }

        var name = Clean(source.Name, 100);
        if (name is null || name.Length < 2)
        {
            warnings.Add("Nama toko di database Aronium kosong atau terlalu pendek; profil toko tidak diubah.");
            return null;
        }

        var address = BuildAddress(source);
        var taxNumber = Clean(source.TaxNumber, 50);
        var email = Clean(source.Email, 200);
        if (email is not null && (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@')))
        {
            email = null;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE company SET
              name = @name,
              tax_number = COALESCE(@taxNumber, tax_number),
              address = COALESCE(@address, address),
              phone = COALESCE(@phone, phone),
              email = COALESCE(@email, email),
              updated_at = @now
            WHERE id = 'company';
            """,
            new
            {
                name,
                taxNumber,
                address,
                phone = Clean(source.PhoneNumber, 50),
                email,
                now = SqlText.Now(now)
            },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return name;
    }

    private async Task<Dictionary<long, string>> ImportGroupsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SourceSnapshot snapshot,
        DateTimeOffset now,
        ImportCounters counters,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, string>();
        var bySourceId = snapshot.Groups
            .Where(group => group.Id != 0)
            .GroupBy(group => group.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var existing = (await connection.QueryAsync<TargetGroup>(
                new CommandDefinition(
                    "SELECT id, name, parent_id FROM product_groups;",
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false))
            .ToList();
        var byKey = existing.ToDictionary(
            group => BuildGroupKey(group.ParentId, group.Name),
            group => group.Id,
            StringComparer.OrdinalIgnoreCase);

        async Task<string?> ResolveAsync(long sourceId, HashSet<long> visiting)
        {
            if (result.TryGetValue(sourceId, out var already))
            {
                return already;
            }

            if (!bySourceId.TryGetValue(sourceId, out var group) ||
                !visiting.Add(sourceId))
            {
                return null;
            }

            string? parentId = null;
            if (group.ParentGroupId is { } parentSourceId && parentSourceId != sourceId)
            {
                parentId = await ResolveAsync(parentSourceId, visiting).ConfigureAwait(false);
            }

            visiting.Remove(sourceId);

            var name = Clean(group.Name, 100);
            if (name is null)
            {
                return parentId;
            }

            var key = BuildGroupKey(parentId, name);
            if (byKey.TryGetValue(key, out var existingId))
            {
                result[sourceId] = existingId;
                return existingId;
            }

            var id = Ids.NewId();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO product_groups
                  (id, name, parent_id, sort_order, active, created_at, updated_at)
                VALUES
                  (@id, @name, @parentId, @sortOrder, 1, @now, @now);
                """,
                new
                {
                    id,
                    name,
                    parentId,
                    sortOrder = ToInt(group.Rank, 0, 0, 1_000_000),
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            byKey[key] = id;
            result[sourceId] = id;
            counters.GroupsCreated++;
            return id;
        }

        foreach (var sourceId in bySourceId.Keys)
        {
            await ResolveAsync(sourceId, []).ConfigureAwait(false);
        }

        return result;
    }

    private static async Task<Dictionary<string, string>> LoadUnitsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<TargetUnit>(
            new CommandDefinition(
                "SELECT id, name FROM units;",
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        var units = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            units.TryAdd(row.Name.Trim(), row.Id);
        }

        return units;
    }

    private async Task<Dictionary<long, string>> ImportTaxesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SourceSnapshot snapshot,
        DateTimeOffset now,
        ImportCounters counters,
        WarningCollector warnings,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, string>();
        var existing = (await connection.QueryAsync<TargetTax>(
                new CommandDefinition(
                    "SELECT id, name, rate_bp FROM tax_rates;",
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false))
            .ToList();
        var byName = existing
            .GroupBy(tax => tax.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var usedNames = existing
            .Select(tax => tax.Name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var source in snapshot.Taxes)
        {
            var name = Clean(source.Name, 100);
            if (name is null)
            {
                continue;
            }

            if (AsLong(source.IsFixed) == 1)
            {
                warnings.Add($"Pajak tetap \"{name}\" dilewati.");
                continue;
            }

            var rateBp = ToInt(
                AsDecimal(source.Rate) * 100m,
                0,
                0,
                100_000,
                MidpointRounding.AwayFromZero);
            if (byName.TryGetValue(name, out var sameName))
            {
                if (sameName.RateBp == rateBp)
                {
                    result[source.Id] = sameName.Id;
                    continue;
                }

                var uniqueName = UniqueName(name, usedNames);
                var uniqueId = await InsertTaxAsync(
                    connection,
                    transaction,
                    uniqueName,
                    rateBp,
                    active: AsLong(source.IsEnabled, 1) != 0,
                    now,
                    cancellationToken).ConfigureAwait(false);
                result[source.Id] = uniqueId;
                usedNames.Add(uniqueName);
                byName[uniqueName] = new TargetTax
                {
                    Id = uniqueId,
                    Name = uniqueName,
                    RateBp = rateBp
                };
                counters.TaxesCreated++;
                continue;
            }

            var id = await InsertTaxAsync(
                connection,
                transaction,
                name,
                rateBp,
                active: AsLong(source.IsEnabled, 1) != 0,
                now,
                cancellationToken).ConfigureAwait(false);
            result[source.Id] = id;
            byName[name] = new TargetTax { Id = id, Name = name, RateBp = rateBp };
            usedNames.Add(name);
            counters.TaxesCreated++;
        }

        return result;
    }

    private static Task<string> InsertTaxAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string name,
        int rateBp,
        bool active,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var id = Ids.NewId();
        return InsertTaxCoreAsync();

        async Task<string> InsertTaxCoreAsync()
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO tax_rates
                  (id, name, rate_bp, active, is_default, created_at, updated_at)
                VALUES
                  (@id, @name, @rateBp, @active, 0, @now, @now);
                """,
                new
                {
                    id,
                    name,
                    rateBp,
                    active = active ? 1 : 0,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            return id;
        }
    }

    private async Task<Dictionary<long, string>> ImportProductsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SourceSnapshot snapshot,
        IReadOnlyDictionary<long, string> groups,
        IDictionary<string, string> units,
        IReadOnlyDictionary<long, string> taxes,
        AroniumImportOptions options,
        int currencyMinorUnit,
        DateTimeOffset now,
        ImportCounters counters,
        WarningCollector warnings,
        CancellationToken cancellationToken)
    {
        var existing = (await connection.QueryAsync<TargetProduct>(
                new CommandDefinition(
                    """
                    SELECT id, name, code, group_id, active, default_qty_milli, is_service,
                           description, cost_minor, markup_bp, price_minor,
                           price_includes_tax, price_change_allowed, color
                    FROM products;
                    """,
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false))
            .ToList();

        var byCode = existing
            .Where(product => !string.IsNullOrWhiteSpace(product.Code))
            .GroupBy(product => product.Code!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var byName = existing
            .GroupBy(product => product.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase);

        var baseUnits = (await connection.QueryAsync<TargetBaseUnit>(
                new CommandDefinition(
                    """
                    SELECT product_id, id, unit_id, price_minor
                    FROM product_units
                    WHERE is_base = 1;
                    """,
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false))
            .GroupBy(unit => unit.ProductId)
            .ToDictionary(group => group.Key, group => group.First());
        var barcodeOwners = (await connection.QueryAsync<TargetBarcodeOwner>(
                new CommandDefinition(
                    """
                    SELECT pu.product_id, b.barcode
                    FROM product_barcodes b
                    JOIN product_units pu ON pu.id = b.product_unit_id;
                    """,
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false))
            .GroupBy(row => row.Barcode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new HashSet<string>(group.Select(row => row.ProductId)),
                StringComparer.OrdinalIgnoreCase);
        var productById = existing
            .GroupBy(product => product.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var productsWithBarcodes = barcodeOwners.Values
            .SelectMany(owners => owners)
            .ToHashSet(StringComparer.Ordinal);

        var barcodesByProduct = snapshot.Barcodes
            .Where(barcode => !string.IsNullOrWhiteSpace(barcode.Value))
            .GroupBy(barcode => barcode.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(barcode => barcode.Value!.Trim())
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList());

        var result = new Dictionary<long, string>();
        var taxLinks = new List<(long SourceProductId, long SourceTaxId)>();

        foreach (var source in snapshot.Products)
        {
            var name = Clean(source.Name, 200);
            if (name is null)
            {
                counters.ProductsSkipped++;
                continue;
            }

            var code = Clean(source.Code, 100);
            var sourceBarcodes = barcodesByProduct.TryGetValue(source.Id, out var barcodes)
                ? barcodes
                : [];

            TargetProduct? match = null;
            if (code is not null && byCode.TryGetValue(code, out var byCodeMatch))
            {
                match = byCodeMatch;
            }
            else
            {
                match = sourceBarcodes
                    .Select(barcode => barcodeOwners.TryGetValue(barcode, out var owners)
                        ? owners
                            .Select(owner => productById.TryGetValue(owner, out var ownerProduct)
                                ? ownerProduct
                                : null)
                            .FirstOrDefault(ownerProduct => ownerProduct is not null)
                        : null)
                    .FirstOrDefault(product => product is not null);
            }

            if (match is null &&
                code is null &&
                sourceBarcodes.Count == 0 &&
                byName.TryGetValue(name, out var nameMatches) &&
                nameMatches.Count == 1)
            {
                match = nameMatches[0];
            }

            var price = ToMinor(AsDecimal(source.Price), currencyMinorUnit);
            var cost = ToMinor(AsDecimal(source.Cost), currencyMinorUnit);
            var markup = AsDecimal(source.Markup);
            var markupBp = markup > 0
                ? ToInt(markup * 100m, 0, 0, 1_000_000, MidpointRounding.AwayFromZero)
                : (int?)null;
            var groupId = source.ProductGroupId is { } sourceGroupId &&
                          groups.TryGetValue(sourceGroupId, out var mappedGroup)
                ? mappedGroup
                : null;
            var age = source.AgeRestriction is null
                ? (int?)null
                : ToInt(AsDecimal(source.AgeRestriction), 0, 0, 100);
            var priceIncludesTax = AsLong(source.IsTaxInclusivePrice, 1) != 0;
            var priceChangeAllowed = AsLong(source.IsPriceChangeAllowed) != 0;
            var isService = AsLong(source.IsService) != 0;
            var active = AsLong(source.IsEnabled, 1) != 0;

            string productId;
            if (match is null)
            {
                productId = Ids.NewId();
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO products
                      (id, name, code, group_id, active, default_qty_milli, is_service,
                       age_restriction, description, cost_minor, markup_bp, price_minor,
                       price_includes_tax, price_change_allowed, color, created_at, updated_at)
                    VALUES
                      (@id, @name, @code, @groupId, @active, 1000, @isService,
                       @age, @description, @cost, @markupBp, @price,
                       @priceIncludesTax, @priceChangeAllowed, @color, @now, @now);
                    """,
                    new
                    {
                        id = productId,
                        name,
                        code,
                        groupId,
                        active = active ? 1 : 0,
                        isService = isService ? 1 : 0,
                        age,
                        description = Clean(source.Description, 500),
                        cost,
                        markupBp,
                        price,
                        priceIncludesTax = priceIncludesTax ? 1 : 0,
                        priceChangeAllowed = priceChangeAllowed ? 1 : 0,
                        color = Clean(source.Color, 50),
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

                byName[name] = [new TargetProduct { Id = productId, Name = name, Code = code }];
                if (code is not null)
                {
                    byCode[code] = byName[name][0];
                }

                counters.ProductsCreated++;
            }
            else
            {
                productId = match.Id;
                if (!options.UpdateExisting)
                {
                    counters.ProductsSkipped++;
                    result[source.Id] = productId;
                    continue;
                }

                if (code is not null &&
                    byCode.TryGetValue(code, out var codeOwner) &&
                    !string.Equals(codeOwner.Id, productId, StringComparison.Ordinal))
                {
                    code = match.Code;
                    warnings.Add($"SKU \"{source.Code}\" sudah dipakai produk lain; SKU produk \"{name}\" tidak diubah.");
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE products SET
                      name = @name, code = COALESCE(@code, code), group_id = @groupId, active = @active,
                      is_service = @isService, age_restriction = @age,
                      description = @description, cost_minor = @cost, markup_bp = @markupBp,
                      price_minor = @price, price_includes_tax = @priceIncludesTax,
                      price_change_allowed = @priceChangeAllowed, color = @color,
                      updated_at = @now
                    WHERE id = @id;
                    """,
                    new
                    {
                        id = productId,
                        name,
                        code,
                        groupId,
                        active = active ? 1 : 0,
                        isService = isService ? 1 : 0,
                        age,
                        description = Clean(source.Description, 500),
                        cost,
                        markupBp,
                        price,
                        priceIncludesTax = priceIncludesTax ? 1 : 0,
                        priceChangeAllowed = priceChangeAllowed ? 1 : 0,
                        color = Clean(source.Color, 50),
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

                counters.ProductsUpdated++;
            }

            result[source.Id] = productId;

            if (!baseUnits.TryGetValue(productId, out var baseUnit))
            {
                var unitName = NormalizeUnitName(source.MeasurementUnit);
                var unitId = await ResolveUnitAsync(
                    connection,
                    transaction,
                    units,
                    unitName,
                    now,
                    counters,
                    cancellationToken).ConfigureAwait(false);
                var newBaseUnitId = Ids.NewId();
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO product_units
                      (id, product_id, unit_id, factor_to_base_milli, is_base, is_default_sale,
                       price_minor, sort_order, active, created_at, updated_at)
                    VALUES
                      (@id, @productId, @unitId, 1000, 1, 1, @price, 0, 1, @now, @now);
                    """,
                    new
                    {
                        id = newBaseUnitId,
                        productId,
                        unitId,
                        price,
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

                baseUnit = new TargetBaseUnit
                {
                    ProductId = productId,
                    Id = newBaseUnitId,
                    UnitId = unitId,
                    PriceMinor = price
                };
                baseUnits[productId] = baseUnit;
            }
            else if (match is not null && options.UpdateExisting)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE product_units
                    SET price_minor = @price, updated_at = @now
                    WHERE id = @id;
                    """,
                    new { id = baseUnit.Id, price, now = SqlText.Now(now) },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            var hasBarcode = productsWithBarcodes.Contains(productId);
            foreach (var barcode in sourceBarcodes)
            {
                if (barcodeOwners.TryGetValue(barcode, out var owners))
                {
                    if (!owners.Contains(productId))
                    {
                        warnings.Add($"Barcode \"{barcode}\" sudah dipakai produk lain dan dilewati.");
                    }

                    continue;
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO product_barcodes
                      (id, product_unit_id, barcode, is_primary, created_at)
                    VALUES
                      (@id, @productUnitId, @barcode, @isPrimary, @now);
                    """,
                    new
                    {
                        id = Ids.NewId(),
                        productUnitId = baseUnit.Id,
                        barcode,
                        isPrimary = hasBarcode ? 0 : 1,
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

                barcodeOwners[barcode] = [productId];
                productsWithBarcodes.Add(productId);
                hasBarcode = true;
                counters.BarcodesCreated++;
            }

            taxLinks.AddRange(snapshot.ProductTaxes
                .Where(link => link.ProductId == source.Id)
                .Select(link => (link.ProductId, link.TaxId)));
        }

        foreach (var (sourceProductId, sourceTaxId) in taxLinks)
        {
            if (!result.TryGetValue(sourceProductId, out var productId) ||
                !taxes.TryGetValue(sourceTaxId, out var taxId))
            {
                continue;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT OR IGNORE INTO product_taxes (product_id, tax_rate_id)
                VALUES (@productId, @taxId);
                """,
                new { productId, taxId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        return result;
    }

    private async Task ImportCustomersAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SourceSnapshot snapshot,
        AroniumImportOptions options,
        DateTimeOffset now,
        ImportCounters counters,
        WarningCollector warnings,
        CancellationToken cancellationToken)
    {
        var existing = (await connection.QueryAsync<TargetCustomer>(
                new CommandDefinition(
                    "SELECT id, code, name, phone FROM customers;",
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false))
            .ToList();
        var byCode = existing
            .Where(customer => !string.IsNullOrWhiteSpace(customer.Code))
            .GroupBy(customer => customer.Code!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var byNamePhone = existing
            .GroupBy(customer => CustomerKey(customer.Name, customer.Phone), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var usedCodes = byCode.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var source in snapshot.Customers)
        {
            var name = Clean(source.Name, 200);
            if (name is null)
            {
                counters.CustomersSkipped++;
                continue;
            }

            var code = Clean(source.Code, 100);
            var phone = Clean(source.PhoneNumber, 50);
            TargetCustomer? match = null;
            if (code is not null && byCode.TryGetValue(code, out var byCodeMatch))
            {
                match = byCodeMatch;
            }
            else if (byNamePhone.TryGetValue(CustomerKey(name, phone), out var byNameMatch))
            {
                match = byNameMatch;
            }

            var type = CustomerType(source);
            var address = BuildAddress(source);

            if (match is not null)
            {
                if (!options.UpdateExisting)
                {
                    counters.CustomersSkipped++;
                    continue;
                }

                if (code is not null &&
                    usedCodes.Contains(code) &&
                    !string.Equals(match.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    code = match.Code;
                    warnings.Add($"Kode pelanggan \"{source.Code}\" sudah dipakai; kode pelanggan \"{name}\" tidak diubah.");
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE customers SET
                      code = COALESCE(@code, code), name = @name, type = @type,
                      tax_number = @taxNumber, address = @address, phone = @phone,
                      email = @email, active = @active, updated_at = @now
                    WHERE id = @id;
                    """,
                    new
                    {
                        id = match.Id,
                        code,
                        name,
                        type,
                        taxNumber = Clean(source.TaxNumber, 50),
                        address,
                        phone,
                        email = Clean(source.Email, 200),
                        active = AsLong(source.IsEnabled, 1) != 0 ? 1 : 0,
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

                counters.CustomersUpdated++;
                continue;
            }

            var id = Ids.NewId();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO customers
                  (id, code, name, type, tax_number, address, phone, email,
                   discount_kind, discount_value, active, notes, created_at, updated_at)
                VALUES
                  (@id, @code, @name, @type, @taxNumber, @address, @phone, @email,
                   'none', 0, @active, 'Diimpor dari Aronium', @now, @now);
                """,
                new
                {
                    id,
                    code,
                    name,
                    type,
                    taxNumber = Clean(source.TaxNumber, 50),
                    address,
                    phone,
                    email = Clean(source.Email, 200),
                    active = AsLong(source.IsEnabled, 1) != 0 ? 1 : 0,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            var created = new TargetCustomer
            {
                Id = id,
                Code = code,
                Name = name,
                Phone = phone
            };
            byNamePhone[CustomerKey(name, phone)] = created;
            if (code is not null)
            {
                byCode[code] = created;
                usedCodes.Add(code);
            }

            counters.CustomersCreated++;
        }
    }

    private async Task ImportPaymentTypesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SourceSnapshot snapshot,
        AroniumImportOptions options,
        DateTimeOffset now,
        ImportCounters counters,
        WarningCollector warnings,
        CancellationToken cancellationToken)
    {
        var existing = (await connection.QueryAsync<TargetPaymentType>(
                new CommandDefinition(
                    "SELECT id, name, shortcut, mark_as_paid, is_default, opens_cash_drawer, active, sort_order FROM payment_types;",
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false))
            .ToList();
        var byName = existing
            .GroupBy(paymentType => paymentType.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var defaultId = existing.FirstOrDefault(paymentType => paymentType.IsDefault == 1)?.Id;

        foreach (var source in snapshot.PaymentTypes)
        {
            var name = Clean(source.Name, 100);
            if (name is null)
            {
                continue;
            }

            var markAsPaid = AsLong(source.MarkAsPaid, 1) != 0;
            var opensDrawer = AsLong(source.OpenCashDrawer) != 0;
            var active = AsLong(source.IsEnabled, 1) != 0;
            var shortcut = Clean(source.ShortcutKey, 20);
            var sortOrder = ToInt(source.Ordinal, 0, 0, 1_000_000);

            if (byName.TryGetValue(name, out var match))
            {
                if (!options.UpdateExisting)
                {
                    continue;
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE payment_types SET
                      shortcut = COALESCE(@shortcut, shortcut),
                      mark_as_paid = @markAsPaid,
                      opens_cash_drawer = @opensDrawer,
                      active = @active,
                      sort_order = @sortOrder,
                      updated_at = @now
                    WHERE id = @id;
                    """,
                    new
                    {
                        id = match.Id,
                        shortcut,
                        markAsPaid = markAsPaid ? 1 : 0,
                        opensDrawer = opensDrawer ? 1 : 0,
                        active = active ? 1 : 0,
                        sortOrder,
                        now = SqlText.Now(now)
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
                counters.PaymentTypesUpdated++;
                continue;
            }

            var id = Ids.NewId();
            var makeDefault = defaultId is null;
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO payment_types
                  (id, name, icon, shortcut, mark_as_paid, is_default,
                   opens_cash_drawer, active, sort_order, created_at, updated_at)
                VALUES
                  (@id, @name, NULL, @shortcut, @markAsPaid, @isDefault,
                   @opensDrawer, @active, @sortOrder, @now, @now);
                """,
                new
                {
                    id,
                    name,
                    shortcut,
                    markAsPaid = markAsPaid ? 1 : 0,
                    isDefault = makeDefault ? 1 : 0,
                    opensDrawer = opensDrawer ? 1 : 0,
                    active = active ? 1 : 0,
                    sortOrder,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (makeDefault)
            {
                defaultId = id;
            }

            byName[name] = new TargetPaymentType { Id = id, Name = name };
            counters.PaymentTypesCreated++;
        }
    }

    private async Task ImportStockAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SourceSnapshot snapshot,
        IReadOnlyDictionary<long, string> products,
        string userId,
        int currencyMinorUnit,
        DateTimeOffset now,
        ImportCounters counters,
        WarningCollector warnings,
        CancellationToken cancellationToken)
    {
        var stockByProduct = snapshot.Stocks
            .GroupBy(stock => stock.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(stock => AsDecimal(stock.Quantity)));
        var sourceProducts = snapshot.Products
            .GroupBy(product => product.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var productsWithMovements = (await connection.QueryAsync<string>(
                new CommandDefinition(
                    "SELECT DISTINCT product_id FROM stock_movements;",
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (sourceProductId, quantity) in stockByProduct)
        {
            if (!products.TryGetValue(sourceProductId, out var productId) ||
                !sourceProducts.TryGetValue(sourceProductId, out var sourceProduct))
            {
                continue;
            }

            if (AsLong(sourceProduct.IsService) != 0 || quantity <= 0)
            {
                continue;
            }

            if (productsWithMovements.Contains(productId))
            {
                counters.StockProductsSkipped++;
                continue;
            }

            var qtyMilli = ToQuantityMilli(quantity);
            if (qtyMilli <= 0)
            {
                continue;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO stock_movements
                  (id, product_id, type, qty_delta_base_milli, unit_cost_minor,
                   reference_type, reference_id, note, user_id, created_at)
                VALUES
                  (@id, @productId, 'opening', @qty, @unitCost,
                   'aronium_import', @referenceId, 'Stok awal dari Aronium', @userId, @now);
                """,
                new
                {
                    id = Ids.NewId(),
                    productId,
                    qty = qtyMilli,
                    unitCost = ToMinor(AsDecimal(sourceProduct.Cost), currencyMinorUnit),
                    referenceId = snapshot.FileName,
                    userId,
                    now = SqlText.Now(now)
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            productsWithMovements.Add(productId);
            counters.StockMovementsCreated++;
        }
    }

    private static async Task<string> ResolveUnitAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IDictionary<string, string> units,
        string name,
        DateTimeOffset now,
        ImportCounters counters,
        CancellationToken cancellationToken)
    {
        var canonical = NormalizeUnitName(name);
        if (units.TryGetValue(canonical, out var existing))
        {
            return existing;
        }

        var id = Ids.NewId();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO units (id, name, symbol, active, created_at, updated_at)
            VALUES (@id, @name, @symbol, 1, @now, @now);
            """,
            new
            {
                id,
                name = canonical,
                symbol = canonical.Length <= 8 ? canonical : canonical[..8],
                now = SqlText.Now(now)
            },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        units[canonical] = id;
        counters.UnitsCreated++;
        return id;
    }

    private static async Task<IReadOnlyList<T>> ReadOptionalTableAsync<T>(
        SqliteConnection connection,
        IReadOnlySet<string> tables,
        string table,
        string sqlTemplate,
        IReadOnlyList<string> optionalColumns,
        CancellationToken cancellationToken)
    {
        if (!tables.Contains(table))
        {
            return [];
        }

        var columns = await ReadColumnsAsync(connection, table, cancellationToken)
            .ConfigureAwait(false);
        var expressions = optionalColumns
            .Select(column => Column(columns, column, column))
            .ToArray();
        var sql = string.Format(CultureInfo.InvariantCulture, sqlTemplate, expressions);
        return (await connection.QueryAsync<T>(new CommandDefinition(
                sql,
                cancellationToken: cancellationToken)).ConfigureAwait(false))
            .ToList();
    }

    private static async Task<HashSet<string>> ReadColumnsAsync(
        SqliteConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        if (!KnownTables.Contains(table, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(table));
        }

        // PRAGMA table_info returns cid, name, type, ...; name the column so
        // Dapper does not try to map the numeric cid.
        var names = await connection.QueryAsync<TableColumn>(new CommandDefinition(
            $"SELECT name AS Name FROM pragma_table_info('{table}');",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return names
            .Select(column => column.Name.Trim())
            .Where(column => column.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string Column(
        IReadOnlySet<string> columns,
        string column,
        string alias) =>
        columns.Contains(column)
            ? $"\"{column}\" AS \"{alias}\""
            : $"NULL AS \"{alias}\"";

    private static void RequireColumns(
        string table,
        IReadOnlySet<string> columns,
        params string[] required)
    {
        foreach (var column in required)
        {
            if (!columns.Contains(column))
            {
                throw new ValidationException(
                    "aronium.schema",
                    $"Skema database Aronium tidak dikenali: kolom {table}.{column} tidak ditemukan.");
            }
        }
    }

    private static async Task<long> CountRowsAsync(
        SqliteConnection connection,
        IReadOnlySet<string> tables,
        string table,
        CancellationToken cancellationToken)
    {
        if (!tables.Contains(table) || !KnownTables.Contains(table, StringComparer.Ordinal))
        {
            return 0;
        }

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            $"SELECT COUNT(*) FROM \"{table}\";",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private string ValidateSourcePath(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ValidationException(
                "aronium.path_required",
                "Pilih file database Aronium terlebih dahulu.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(sourcePath.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw new ValidationException("aronium.path_invalid", "Lokasi file database tidak valid.");
        }

        if (!File.Exists(fullPath))
        {
            throw new ValidationException(
                "aronium.file_missing",
                "File database Aronium tidak ditemukan. Periksa kembali lokasi pos.db.");
        }

        if (string.Equals(
                fullPath,
                Path.GetFullPath(_factory.DatabasePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                "aronium.same_database",
                "File yang dipilih adalah database aplikasi ini, bukan database Aronium.");
        }

        var header = new byte[4096];
        using (var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var read = stream.Read(header, 0, header.Length);
            if (read >= 16 &&
                header.AsSpan(0, 16).SequenceEqual("SQLite format 3\u0000"u8))
            {
                return fullPath;
            }

            var text = Encoding.ASCII.GetString(header, 0, read);
            if (fullPath.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("TAPE", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Microsoft SQL Server", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("MSSQL", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException(
                    "aronium.sql_server_backup",
                    "File .bak adalah cadangan Aronium Pro (SQL Server), bukan database SQLite Aronium Lite. " +
                    "Impor langsung belum dapat membaca file ini. Ekspor produk dan pelanggan ke CSV dari Aronium, " +
                    "atau gunakan file pos.db dari Aronium Lite.");
            }
        }

        throw new ValidationException(
            "aronium.not_sqlite",
            "File yang dipilih bukan database SQLite Aronium Lite. Pilih file pos.db dari folder Aronium\\Data.");
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

    private static decimal AsDecimal(object? value)
    {
        if (value is null || value is DBNull)
        {
            return 0m;
        }

        try
        {
            return value switch
            {
                decimal number => number,
                double number => (decimal)number,
                float number => (decimal)number,
                long number => number,
                int number => number,
                short number => number,
                byte number => number,
                string text when decimal.TryParse(
                    text,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsed) => parsed,
                _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
            };
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return 0m;
        }
    }

    private static long AsLong(object? value, long defaultValue = 0)
    {
        if (value is null || value is DBNull)
        {
            return defaultValue;
        }

        try
        {
            return value switch
            {
                long number => number,
                int number => number,
                short number => number,
                byte number => number,
                bool boolean => boolean ? 1 : 0,
                decimal number => (long)number,
                double number => (long)number,
                float number => (long)number,
                string text when long.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed) => parsed,
                _ => Convert.ToInt64(value, CultureInfo.InvariantCulture)
            };
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return defaultValue;
        }
    }

    private static long ToMinor(decimal value, int currencyMinorUnit)
    {
        if (value <= 0)
        {
            return 0;
        }

        decimal factor = currencyMinorUnit switch
        {
            1 => 10m,
            2 => 100m,
            3 => 1_000m,
            4 => 10_000m,
            _ => 1m
        };
        var scaled = decimal.Round(value * factor, 0, MidpointRounding.AwayFromZero);
        if (scaled >= long.MaxValue)
        {
            return long.MaxValue;
        }

        return (long)scaled;
    }

    private static long ToQuantityMilli(decimal quantity)
    {
        if (quantity <= 0)
        {
            return 0;
        }

        var scaled = decimal.Round(quantity * 1000m, 0, MidpointRounding.AwayFromZero);
        return scaled >= long.MaxValue ? long.MaxValue : (long)scaled;
    }

    private static int ToInt(
        object? value,
        int defaultValue,
        int minimum,
        int maximum,
        MidpointRounding rounding = MidpointRounding.ToEven)
    {
        var decimalValue = AsDecimal(value);
        var rounded = (int)Math.Clamp(
            decimal.Round(decimalValue, 0, rounding),
            minimum,
            maximum);
        return rounded == 0 && value is null ? defaultValue : rounded;
    }

    private static int ToInt(
        decimal value,
        int defaultValue,
        int minimum,
        int maximum,
        MidpointRounding rounding = MidpointRounding.ToEven)
    {
        var rounded = (int)Math.Clamp(decimal.Round(value, 0, rounding), minimum, maximum);
        return rounded == 0 && value == 0 ? defaultValue : rounded;
    }

    private static string? Clean(string? value, int maximumLength)
    {
        var cleaned = value?.Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        return cleaned.Length <= maximumLength ? cleaned : cleaned[..maximumLength];
    }

    private static string BuildGroupKey(string? parentId, string name) =>
        (parentId ?? string.Empty) + "\u001f" + name.Trim().ToLowerInvariant();

    private static string CustomerKey(string name, string? phone) =>
        name.Trim().ToLowerInvariant() + "\u001f" + (phone ?? string.Empty).Trim().ToLowerInvariant();

    private static string UniqueName(string name, IReadOnlySet<string> used)
    {
        if (!used.Contains(name))
        {
            return name;
        }

        for (var suffix = 2; suffix < 10_000; suffix++)
        {
            var candidate = $"{name} ({suffix})";
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }

        return name + " (Aronium)";
    }

    private static string NormalizeUnitName(string? value)
    {
        var cleaned = value?.Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "pcs";
        }

        var normalized = cleaned.ToLowerInvariant();
        var canonical = normalized switch
        {
            "pc" or "pcs" or "pcs." or "piece" or "pieces" or "ea" or "each" or "unit" or "units" => "pcs",
            "kilogram" or "kilograms" or "kgs" or "kg." => "kg",
            "gram" or "grams" or "gr" or "grm" or "g." => "g",
            "liter" or "liters" or "litre" or "litres" or "ltr" or "lt" => "L",
            "mililiter" or "milliliter" or "milliliters" or "ml." => "mL",
            "dozen" or "dozens" => "lusin",
            _ => cleaned
        };

        return canonical.Length <= 30 ? canonical : canonical[..30];
    }

    private static bool IsCustomer(SourceCustomer customer) =>
        AsLong(customer.IsCustomer, 1) != 0;

    private static bool IsSupplier(SourceCustomer customer) =>
        AsLong(customer.IsSupplier, 1) != 0;

    private static string CustomerType(SourceCustomer customer)
    {
        var isCustomer = IsCustomer(customer);
        var isSupplier = IsSupplier(customer);
        return (isCustomer, isSupplier) switch
        {
            (true, true) => "both",
            (false, true) => "supplier",
            _ => "customer"
        };
    }

    private static string? BuildAddress(SourceCustomer customer)
    {
        var explicitAddress = Clean(customer.Address, 500);
        var locality = JoinNonEmpty(
            Clean(customer.City, 100),
            Clean(customer.PostalCode, 20));
        var street = JoinNonEmpty(
            Clean(customer.StreetName, 200),
            Clean(customer.BuildingNumber, 30));
        if (street is not null)
        {
            street = JoinNonEmpty(street, Clean(customer.CitySubdivisionName, 100));
            street = JoinNonEmpty(street, Clean(customer.CountrySubentity, 100));
        }

        if (explicitAddress is not null)
        {
            if (locality is not null &&
                !explicitAddress.Contains(locality, StringComparison.OrdinalIgnoreCase))
            {
                return Clean(explicitAddress + ", " + locality, 500);
            }

            return explicitAddress;
        }

        return Clean(JoinNonEmpty(street, locality), 500);
    }

    private static string? BuildAddress(SourceCompany company)
    {
        var explicitAddress = Clean(company.Address, 500);
        var locality = JoinNonEmpty(
            Clean(company.City, 100),
            Clean(company.PostalCode, 20));
        if (explicitAddress is not null)
        {
            if (locality is not null &&
                !explicitAddress.Contains(locality, StringComparison.OrdinalIgnoreCase))
            {
                return Clean(explicitAddress + ", " + locality, 500);
            }

            return explicitAddress;
        }

        return locality;
    }

    private static string? JoinNonEmpty(params string?[] parts)
    {
        var values = parts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim())
            .ToArray();
        return values.Length == 0 ? null : string.Join(", ", values);
    }

    private sealed class SourceSnapshot(string filePath, string fileName, long fileSizeBytes)
    {
        public string FilePath { get; } = filePath;
        public string FileName { get; } = fileName;
        public long FileSizeBytes { get; } = fileSizeBytes;
        public IReadOnlyList<SourceProduct> Products { get; set; } = [];
        public IReadOnlyList<SourceProductGroup> Groups { get; set; } = [];
        public IReadOnlyList<SourceBarcode> Barcodes { get; set; } = [];
        public IReadOnlyList<SourceTax> Taxes { get; set; } = [];
        public IReadOnlyList<SourceProductTax> ProductTaxes { get; set; } = [];
        public IReadOnlyList<SourceCustomer> Customers { get; set; } = [];
        public IReadOnlyList<SourcePaymentType> PaymentTypes { get; set; } = [];
        public IReadOnlyList<SourceStock> Stocks { get; set; } = [];
        public IReadOnlyList<SourceCompany> Companies { get; set; } = [];
        public long DocumentCount { get; set; }
        public long CustomerDiscountCount { get; set; }
        public long ProductImageCount { get; set; }
        public IReadOnlyList<string> Warnings { get; set; } = [];
    }

    private sealed class ImportCounters
    {
        public int ProductsCreated { get; set; }
        public int ProductsUpdated { get; set; }
        public int ProductsSkipped { get; set; }
        public int GroupsCreated { get; set; }
        public int UnitsCreated { get; set; }
        public int BarcodesCreated { get; set; }
        public int TaxesCreated { get; set; }
        public int CustomersCreated { get; set; }
        public int CustomersUpdated { get; set; }
        public int CustomersSkipped { get; set; }
        public int PaymentTypesCreated { get; set; }
        public int PaymentTypesUpdated { get; set; }
        public int StockMovementsCreated { get; set; }
        public int StockProductsSkipped { get; set; }
    }

    private sealed class WarningCollector
    {
        private const int MaximumWarnings = 40;
        private readonly List<string> _warnings = [];
        private int _suppressed;

        public WarningCollector()
        {
        }

        public WarningCollector(IEnumerable<string> warnings)
        {
            foreach (var warning in warnings)
            {
                Add(warning);
            }
        }

        public void Add(string warning)
        {
            if (string.IsNullOrWhiteSpace(warning))
            {
                return;
            }

            if (_warnings.Contains(warning, StringComparer.Ordinal))
            {
                return;
            }

            if (_warnings.Count >= MaximumWarnings)
            {
                _suppressed++;
                return;
            }

            _warnings.Add(warning);
        }

        public IReadOnlyList<string> Build()
        {
            if (_suppressed == 0)
            {
                return _warnings;
            }

            var result = new List<string>(_warnings)
            {
                $"… dan {_suppressed:N0} peringatan lain yang serupa."
            };
            return result;
        }
    }

    private sealed class TableColumn
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class SourceProduct
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public string? Code { get; set; }
        public long? ProductGroupId { get; set; }
        public string? MeasurementUnit { get; set; }
        public object? Price { get; set; }
        public object? Cost { get; set; }
        public object? Markup { get; set; }
        public object? AgeRestriction { get; set; }
        public object? IsTaxInclusivePrice { get; set; }
        public object? IsPriceChangeAllowed { get; set; }
        public object? IsService { get; set; }
        public object? IsUsingDefaultQuantity { get; set; }
        public object? IsEnabled { get; set; }
        public string? Description { get; set; }
        public string? Color { get; set; }
        public object? Plu { get; set; }
    }

    private sealed class SourceProductGroup
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public long? ParentGroupId { get; set; }
        public object? Rank { get; set; }
    }

    private sealed class SourceBarcode
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public string? Value { get; set; }
    }

    private sealed class SourceTax
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public object? Rate { get; set; }
        public object? IsEnabled { get; set; }
        public object? IsFixed { get; set; }
    }

    private sealed class SourceProductTax
    {
        public long ProductId { get; set; }
        public long TaxId { get; set; }
    }

    private sealed class SourceCustomer
    {
        public long Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? TaxNumber { get; set; }
        public string? Address { get; set; }
        public string? PostalCode { get; set; }
        public string? City { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public object? IsEnabled { get; set; }
        public object? IsCustomer { get; set; }
        public object? IsSupplier { get; set; }
        public string? StreetName { get; set; }
        public string? BuildingNumber { get; set; }
        public string? CitySubdivisionName { get; set; }
        public string? CountrySubentity { get; set; }
    }

    private sealed class SourcePaymentType
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public string? ShortcutKey { get; set; }
        public object? IsEnabled { get; set; }
        public object? OpenCashDrawer { get; set; }
        public object? MarkAsPaid { get; set; }
        public object? Ordinal { get; set; }
    }

    private sealed class SourceStock
    {
        public long ProductId { get; set; }
        public object? Quantity { get; set; }
    }

    private sealed class SourceCompany
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? PostalCode { get; set; }
        public string? City { get; set; }
        public string? TaxNumber { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
    }

    private sealed class TargetCompany
    {
        public string Name { get; set; } = string.Empty;
        public string? LegalName { get; set; }
        public string? TaxNumber { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }

    private sealed class TargetGroup
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ParentId { get; set; }
    }

    private sealed class TargetUnit
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TargetTax
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int RateBp { get; set; }
    }

    private sealed class TargetProduct
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? GroupId { get; set; }
        public int Active { get; set; }
        public int DefaultQtyMilli { get; set; }
        public int IsService { get; set; }
        public string? Description { get; set; }
        public long CostMinor { get; set; }
        public int? MarkupBp { get; set; }
        public long PriceMinor { get; set; }
        public int PriceIncludesTax { get; set; }
        public int PriceChangeAllowed { get; set; }
        public string? Color { get; set; }
    }

    private sealed class TargetBaseUnit
    {
        public string ProductId { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string UnitId { get; set; } = string.Empty;
        public long? PriceMinor { get; set; }
    }

    private sealed class TargetBarcodeOwner
    {
        public string ProductId { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
    }

    private sealed class TargetCustomer
    {
        public string Id { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }

    private sealed class TargetPaymentType
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Shortcut { get; set; }
        public int MarkAsPaid { get; set; }
        public int IsDefault { get; set; }
        public int OpensCashDrawer { get; set; }
        public int Active { get; set; }
        public int SortOrder { get; set; }
    }
}

/// <summary>Read-only summary of an Aronium Lite database.</summary>
public sealed record AroniumImportPreview(
    string FilePath,
    string FileName,
    long FileSizeBytes,
    int ProductCount,
    int GroupCount,
    int BarcodeCount,
    int TaxCount,
    int CustomerCount,
    int SupplierCount,
    int PaymentTypeCount,
    int StockProductCount,
    long DocumentCount,
    IReadOnlyList<string> Warnings)
{
    public bool CanImport => ProductCount > 0;
}

/// <summary>Options controlling which Aronium data is merged into this shop.</summary>
public sealed record AroniumImportOptions(
    string FilePath,
    bool ImportCompany = true,
    bool ImportCustomers = true,
    bool ImportPaymentTypes = true,
    bool ImportStock = false,
    bool UpdateExisting = true);

/// <summary>Counts and warnings produced by an Aronium import.</summary>
public sealed record AroniumImportResult(
    int ProductsCreated,
    int ProductsUpdated,
    int ProductsSkipped,
    int GroupsCreated,
    int UnitsCreated,
    int BarcodesCreated,
    int TaxesCreated,
    int CustomersCreated,
    int CustomersUpdated,
    int CustomersSkipped,
    int PaymentTypesCreated,
    int PaymentTypesUpdated,
    int StockMovementsCreated,
    int StockProductsSkipped,
    bool CompanyUpdated,
    string? CompanyName,
    IReadOnlyList<string> Warnings);
