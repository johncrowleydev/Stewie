using System.Text;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Stewie.Api.Middleware;
using Stewie.Application.Interfaces;
using Stewie.Application.Services;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Stewie.Infrastructure.Persistence;
using Stewie.Infrastructure.Repositories;
using Stewie.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var connectionString = builder.Configuration.GetConnectionString("Stewie")
    ?? throw new InvalidOperationException("Connection string 'Stewie' is required.");
var workspaceRoot = builder.Configuration.GetValue<string>("Stewie:WorkspaceRoot") ?? "./workspaces";
var dockerImageName = builder.Configuration.GetValue<string>("Stewie:DockerImageName") ?? "stewie-dummy-worker";
var scriptWorkerImage = builder.Configuration.GetValue<string>("Stewie:ScriptWorkerImage") ?? "stewie-script-worker";
var jwtSecret = builder.Configuration["Stewie:JwtSecret"]
    ?? throw new InvalidOperationException("JWT secret is required. Set Stewie:JwtSecret or STEWIE_JWT_SECRET.");
var encryptionKey = builder.Configuration["Stewie:EncryptionKey"]
    ?? throw new InvalidOperationException("Encryption key is required. Set Stewie:EncryptionKey or STEWIE_ENCRYPTION_KEY.");

// Controllers & OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// JWT Authentication — T-038
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "stewie",
            ValidateAudience = true,
            ValidAudience = "stewie",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            NameClaimType = "sub"
        };
    });
builder.Services.AddAuthorization();

// Ensure database exists before anything tries to connect
DatabaseInitializer.EnsureDatabaseExists(connectionString);

// FluentMigrator — run migrations before NHibernate session factory
builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddSqlServer()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(Stewie.Infrastructure.Persistence.NHibernateHelper).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

// Build a temporary service provider to run migrations now
using (var tempProvider = builder.Services.BuildServiceProvider())
{
    var runner = tempProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp();
}

// NHibernate — build session factory after DB and schema are ready
var sessionFactory = NHibernateHelper.BuildSessionFactory(connectionString);
builder.Services.AddSingleton(sessionFactory);
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Repositories
builder.Services.AddScoped<IRunRepository, RunRepository>();
builder.Services.AddScoped<IWorkTaskRepository, WorkTaskRepository>();
builder.Services.AddScoped<IArtifactRepository, ArtifactRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IWorkspaceRepository, WorkspaceEntityRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IInviteCodeRepository, InviteCodeRepository>();
builder.Services.AddScoped<IUserCredentialRepository, UserCredentialRepository>();

// Services
builder.Services.AddSingleton<IWorkspaceService>(sp =>
    new WorkspaceService(workspaceRoot, sp.GetRequiredService<ILogger<WorkspaceService>>()));
builder.Services.AddSingleton<IContainerService>(sp =>
    new DockerContainerService(dockerImageName, sp.GetRequiredService<ILogger<DockerContainerService>>()));
builder.Services.AddSingleton<IEncryptionService>(new AesEncryptionService(encryptionKey));
builder.Services.AddScoped<IGitHubService, GitHubService>();
builder.Services.AddScoped<RunOrchestrationService>(sp =>
    new RunOrchestrationService(
        sp.GetRequiredService<IRunRepository>(),
        sp.GetRequiredService<IWorkTaskRepository>(),
        sp.GetRequiredService<IArtifactRepository>(),
        sp.GetRequiredService<IEventRepository>(),
        sp.GetRequiredService<IWorkspaceRepository>(),
        sp.GetRequiredService<IProjectRepository>(),
        sp.GetRequiredService<IUserCredentialRepository>(),
        sp.GetRequiredService<IWorkspaceService>(),
        sp.GetRequiredService<IContainerService>(),
        sp.GetRequiredService<IGitHubService>(),
        sp.GetRequiredService<IEncryptionService>(),
        sp.GetRequiredService<IUnitOfWork>(),
        sp.GetRequiredService<ILogger<RunOrchestrationService>>(),
        scriptWorkerImage));

var app = builder.Build();

// Seed admin user on startup if no users exist — T-037
await SeedAdminUserAsync(app);

// Error handling middleware — must be first in the pipeline to catch all exceptions
// REF: CON-002 §6, GOV-004
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication(); // Must be before UseAuthorization
app.UseAuthorization();
app.MapControllers();
app.Run();

/// <summary>Seeds the first admin user if no users exist in the database.</summary>
static async Task SeedAdminUserAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (await userRepo.ExistsAsync()) return;

    var username = config["Stewie:AdminUsername"] ?? "admin";
    var password = config["Stewie:AdminPassword"]
        ?? throw new InvalidOperationException("Admin password required for first-time setup.");

    var admin = new User
    {
        Id = Guid.NewGuid(),
        Username = username,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        Role = UserRole.Admin,
        CreatedAt = DateTime.UtcNow
    };

    unitOfWork.BeginTransaction();
    await userRepo.SaveAsync(admin);
    await unitOfWork.CommitAsync();

    logger.LogInformation("Seeded admin user: {Username}", username);
}

// Required for WebApplicationFactory<Program> in integration tests (GOV-002)
public partial class Program { }
