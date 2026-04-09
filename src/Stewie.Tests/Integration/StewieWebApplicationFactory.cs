/// <summary>
/// WebApplicationFactory fixture for integration tests.
/// Replaces SQL Server with SQLite in-memory database.
/// Bypasses FluentMigrator and DatabaseInitializer by using NHibernate SchemaExport.
///
/// REF: GOV-002 (testing), SPR-002 T-023/T-024
/// </summary>
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Tool.hbm2ddl;
using Stewie.Application.Interfaces;
using Stewie.Infrastructure.Persistence;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Stewie.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory that swaps SQL Server for SQLite in-memory.
/// The SQLite connection is kept alive for the lifetime of the factory
/// so tables persist across requests within a single test.
/// </summary>
public class StewieWebApplicationFactory : WebApplicationFactory<Program>
{
    private ISessionFactory? _sessionFactory;
    private System.Data.SQLite.SQLiteConnection? _keepAliveConnection;

    public StewieWebApplicationFactory()
    {
        System.Environment.SetEnvironmentVariable("Stewie__JwtSecret", "test-jwt-secret-minimum-32-characters-long!!");
        System.Environment.SetEnvironmentVariable("Stewie__EncryptionKey", "dGVzdC1lbmNyeXB0aW9uLWtleS1taW5pbXVtMzJjaHI=");
        System.Environment.SetEnvironmentVariable("Stewie__AdminPassword", "Admin@Stewie123!");
        System.Environment.SetEnvironmentVariable("Stewie__AdminUsername", "admin");
    }

    /// <summary>
    /// Generates a signed JWT for API tests that require authentication.
    /// Uses the same secret bound to the mock environment.
    /// </summary>
    public string GetAuthToken()
    {
        var secret = "test-jwt-secret-minimum-32-characters-long!!";
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "00000000-0000-0000-0000-000000000000"),
            new Claim("username", "admin"),
            new Claim("role", "admin")
        };

        var token = new JwtSecurityToken(
            issuer: "stewie",
            audience: "stewie",
            claims: claims,
            expires: System.DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Remove the production NHibernate session factory
            var sfDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(ISessionFactory));
            if (sfDescriptor is not null) services.Remove(sfDescriptor);

            // Remove the production UnitOfWork
            var uowDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(IUnitOfWork));
            if (uowDescriptor is not null) services.Remove(uowDescriptor);

            // Remove FluentMigrator services (they require SQL Server)
            var fmDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("FluentMigrator") == true
                         || d.ImplementationType?.FullName?.Contains("FluentMigrator") == true)
                .ToList();
            foreach (var d in fmDescriptors) services.Remove(d);

            // Create a persistent SQLite in-memory connection
            _keepAliveConnection = new System.Data.SQLite.SQLiteConnection(
                "Data Source=:memory:;Version=3;New=True;");
            _keepAliveConnection.Open();

            // Build NHibernate session factory with SQLite
            var nhConfig = Fluently.Configure()
                .Database(SQLiteConfiguration.Standard
                    .ConnectionString("Data Source=:memory:;Version=3;New=True;"))
                .Mappings(m => m.FluentMappings
                    .AddFromAssemblyOf<Stewie.Infrastructure.Mappings.RunMap>())
                .BuildConfiguration();

            // Export schema to the keep-alive connection
            var schemaExport = new SchemaExport(nhConfig);
            schemaExport.Execute(false, true, false, _keepAliveConnection, null);

            // Build session factory that reuses the keep-alive connection
            _sessionFactory = nhConfig.BuildSessionFactory();

            services.AddSingleton(_sessionFactory);
            services.AddScoped<IUnitOfWork>(sp =>
            {
                var session = _sessionFactory.OpenSession(_keepAliveConnection!);
                return new TestUnitOfWork(session);
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _sessionFactory?.Dispose();
            _keepAliveConnection?.Dispose();
        }
    }
}

/// <summary>
/// Test UnitOfWork that wraps a provided NHibernate session.
/// Unlike the production UnitOfWork, this one does NOT open a new session — it uses
/// the one bound to the shared SQLite keep-alive connection.
/// </summary>
internal class TestUnitOfWork : IUnitOfWork
{
    private NHibernate.ITransaction? _transaction;

    public TestUnitOfWork(NHibernate.ISession session)
    {
        Session = session;
    }

    public NHibernate.ISession Session { get; }

    public void BeginTransaction()
    {
        _transaction = Session.BeginTransaction();
    }

    public async Task CommitAsync()
    {
        if (_transaction is { IsActive: true })
        {
            await _transaction.CommitAsync();
        }
    }

    public void Rollback()
    {
        if (_transaction is { IsActive: true })
        {
            _transaction.Rollback();
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        Session.Dispose();
    }
}
