using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Data.Database;
using Pos.Data.Security;
using Pos.Data.Services;
using Pos.Domain.Models;
using Pos.Domain.Time;

namespace Pos.Data.Tests;

public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan duration) => UtcNow += duration;
}

public sealed class TestDatabase : IAsyncDisposable
{
    private TestDatabase()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "pos-tests", Guid.NewGuid().ToString("N"));
        Options = new DatabaseOptions(Path.Combine(RootPath, "pos.db"), Path.Combine(RootPath, "backups"));
        Clock = new TestClock();
    }

    public string RootPath { get; }

    public DatabaseOptions Options { get; }

    public SqliteConnectionFactory Factory { get; private set; } = null!;

    public TestClock Clock { get; }

    public SessionStore Sessions { get; private set; } = null!;

    public Argon2PasswordHasher Hasher { get; private set; } = null!;

    public DatabaseBootstrapper Bootstrapper { get; private set; } = null!;

    public AuthService Auth { get; private set; } = null!;

    public SettingsService Settings { get; private set; } = null!;

    public CompanyService Company { get; private set; } = null!;

    public AdminUserService Users { get; private set; } = null!;

    public BackupService Backups { get; private set; } = null!;

    public SaleService Sale { get; private set; } = null!;

    public TaxService Tax { get; private set; } = null!;

    public CreditPaymentService CreditPayment { get; private set; } = null!;

    public ProductService Product { get; private set; } = null!;

    public UnitService Unit { get; private set; } = null!;

    public PriceListService PriceList { get; private set; } = null!;

    public CustomerService Customer { get; private set; } = null!;

    public RegisterService Register { get; private set; } = null!;

    public CashMovementService CashMovements { get; private set; } = null!;

    public RefundService Refunds { get; private set; } = null!;

    public EndOfDayService EndOfDay { get; private set; } = null!;

    public PaymentTypeService PaymentTypes { get; private set; } = null!;

    public DatabaseService Database { get; private set; } = null!;

    public static async Task<TestDatabase> CreateAsync()
    {
        var database = new TestDatabase();
        Directory.CreateDirectory(database.RootPath);
        database.Factory = new SqliteConnectionFactory(database.Options.DatabasePath);
        var migrations = new MigrationRunner(database.Factory, NullLogger<MigrationRunner>.Instance);
        database.Bootstrapper = new DatabaseBootstrapper(
            database.Factory,
            migrations,
            database.Options,
            NullLogger<DatabaseBootstrapper>.Instance);
        await database.Bootstrapper.InitializeAsync();

        database.Sessions = new SessionStore();
        database.Hasher = new Argon2PasswordHasher(Argon2Options.FastForTests);
        database.Auth = new AuthService(
            database.Factory,
            database.Hasher,
            database.Clock,
            database.Sessions,
            NullLogger<AuthService>.Instance);
        database.Settings = new SettingsService(database.Factory, database.Sessions, database.Clock);
        database.Company = new CompanyService(database.Factory, database.Sessions, database.Clock);
        database.Users = new AdminUserService(
            database.Factory,
            database.Sessions,
            database.Hasher,
            database.Clock);
        database.Backups = new BackupService(
            database.Factory,
            database.Options,
            database.Clock,
            NullLogger<BackupService>.Instance);
        database.Tax = new TaxService(
            database.Factory,
            database.Sessions,
            database.Clock);
        database.CreditPayment = new CreditPaymentService(
            database.Factory,
            database.Sessions,
            database.Clock);
        database.Sale = new SaleService(
            database.Factory,
            database.Sessions,
            database.Clock,
            database.Tax,
            database.CreditPayment);
        database.Product = new ProductService(
            database.Factory,
            database.Sessions,
            database.Clock);
        database.Unit = new UnitService(
            database.Factory,
            database.Sessions,
            database.Clock);
        database.PriceList = new PriceListService(
            database.Factory,
            database.Sessions,
            database.Clock);
        database.Customer = new CustomerService(
            database.Factory,
            database.Sessions,
            database.Clock);
        database.Register = new RegisterService(
            database.Factory,
            database.Sessions,
            database.Clock);
        database.CashMovements = new CashMovementService(
            database.Factory,
            database.Sessions,
            database.Clock);
        database.Refunds = new RefundService(
            database.Factory,
            database.Sessions,
            database.Clock,
            database.Sale);
        database.EndOfDay = new EndOfDayService(
            database.Factory,
            database.Sessions,
            database.Clock);
        database.PaymentTypes = new PaymentTypeService(
            database.Factory,
            database.Sessions,
            database.Clock);
        database.Database = new DatabaseService(
            database.Factory,
            database.Sessions,
            database.Clock,
            database.Options);

        return database;
    }

    public Task<SqliteConnection> OpenAsync() => Factory.OpenAsync();

    public Task<UserSession> CreateAdminAsync(
        string username = "admin",
        string password = "Admin1234",
        string shopName = "Toko Uji",
        string fullName = "Admin Uji") =>
        Auth.CreateFirstAdminAsync(shopName, fullName, username, password);

    public async Task<UserSession> CreateUserAndLoginAsync(
        string roleId,
        string username,
        string password,
        string fullName = "Pengguna Uji")
    {
        var hash = await Hasher.HashAsync(password);
        var id = Pos.Domain.Ids.NewId();
        var now = Pos.Data.Services.SqlText.Now(Clock.UtcNow);
        await using var connection = await OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO users (id, username, password_hash, full_name, role_id, active, created_at, updated_at)
                VALUES ($id, $username, $hash, $fullName, $roleId, 1, $now, $now);
                """;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$username", username);
            command.Parameters.AddWithValue("$hash", hash);
            command.Parameters.AddWithValue("$fullName", fullName);
            command.Parameters.AddWithValue("$roleId", roleId);
            command.Parameters.AddWithValue("$now", now);
            await command.ExecuteNonQueryAsync();
        }

        return await Auth.LoginAsync(username, password);
    }

    public async ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        await Task.Delay(20);
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup for a temp test database.
        }
    }
}
