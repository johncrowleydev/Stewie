using System.Text;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Stewie.Api.Middleware;
using Stewie.Application.Configuration;
using Stewie.Application.Hubs;
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
    ?? (builder.Environment.IsEnvironment("Testing")
        ? "Data Source=unused;Initial Catalog=unused"
        : throw new InvalidOperationException("Connection string 'Stewie' is required."));
var workspaceRoot = builder.Configuration.GetValue<string>("Stewie:WorkspaceRoot") ?? "./workspaces";
var dockerImageName = builder.Configuration.GetValue<string>("Stewie:DockerImageName") ?? "stewie-dummy-worker";
var scriptWorkerImage = builder.Configuration.GetValue<string>("Stewie:ScriptWorkerImage") ?? "stewie-script-worker";
var governanceWorkerImage = builder.Configuration.GetValue<string>("Stewie:GovernanceWorkerImage") ?? "stewie-governance-worker";
var maxGovernanceRetries = builder.Configuration.GetValue<int>("Stewie:MaxGovernanceRetries", 2);
var taskTimeoutSeconds = builder.Configuration.GetValue<int>("Stewie:TaskTimeoutSeconds", 300);
var maxConcurrentTasks = builder.Configuration.GetValue<int>("Stewie:MaxConcurrentTasks", 5);
var jwtSecret = builder.Configuration["Stewie:JwtSecret"]
    ?? throw new InvalidOperationException("JWT secret is required. Set Stewie:JwtSecret or STEWIE_JWT_SECRET.");
var encryptionKey = builder.Configuration["Stewie:EncryptionKey"]
    ?? throw new InvalidOperationException("Encryption key is required. Set Stewie:EncryptionKey or STEWIE_ENCRYPTION_KEY.");

// Controllers & OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// RabbitMQ configuration — JOB-016 T-155
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));

builder.Services.AddSingleton(sp => 
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RabbitMqOptions>>().Value;
    return new Stewie.Infrastructure.Services.RabbitMqSettings
    {
        HostName = options.HostName,
        Port = options.Port,
        UserName = options.UserName,
        Password = options.Password,
        VirtualHost = options.VirtualHost,
        MaxRetryAttempts = options.RetryCount,
        RetryBaseDelayMs = options.RetryDelaySeconds * 1000
    };
});

builder.Services.AddSingleton<Stewie.Infrastructure.Services.RabbitMqService>();
builder.Services.AddSingleton<Stewie.Application.Interfaces.IRabbitMqService>(
    sp => sp.GetRequiredService<Stewie.Infrastructure.Services.RabbitMqService>());
builder.Services.AddHostedService<Stewie.Infrastructure.Services.RabbitMqConsumerHostedService>();


// CORS — required for SignalR WebSocket upgrade from frontend dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("StewieCors", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5275")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();  // Required for SignalR
    });
});

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

        // SignalR sends JWT via query string during WebSocket handshake
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Database + migration bootstrap — skipped in Testing (integration tests use SQLite)
if (!builder.Environment.IsEnvironment("Testing"))
{
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
}

// Repositories
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IWorkTaskRepository, WorkTaskRepository>();
builder.Services.AddScoped<IArtifactRepository, ArtifactRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IWorkspaceRepository, WorkspaceEntityRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IInviteCodeRepository, InviteCodeRepository>();
builder.Services.AddScoped<IUserCredentialRepository, UserCredentialRepository>();
builder.Services.AddScoped<IGovernanceReportRepository, GovernanceReportRepository>();
builder.Services.AddScoped<ITaskDependencyRepository, TaskDependencyRepository>();
builder.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
builder.Services.AddScoped<IAgentSessionRepository, AgentSessionRepository>();

// Services
builder.Services.AddSingleton<IWorkspaceService>(sp =>
    new WorkspaceService(workspaceRoot, sp.GetRequiredService<ILogger<WorkspaceService>>()));
builder.Services.AddScoped<IArtifactWorkspaceStore>(sp =>
    new LocalDiskArtifactStore(workspaceRoot, sp.GetRequiredService<ILogger<LocalDiskArtifactStore>>()));
builder.Services.AddSingleton<IContainerService>(sp =>
    new DockerContainerService(dockerImageName, taskTimeoutSeconds, sp.GetRequiredService<ILogger<DockerContainerService>>()));
builder.Services.AddSingleton<IEncryptionService>(new AesEncryptionService(encryptionKey));
builder.Services.AddScoped<IGitPlatformService, GitHubService>();
builder.Services.AddScoped<GovernanceAnalyticsService>();
builder.Services.AddScoped<ProjectConfigService>();
builder.Services.AddSingleton<IRealTimeNotifier, SignalRNotifier>();
builder.Services.AddSingleton<ContainerOutputBuffer>();

// Agent lifecycle — JOB-017 T-165
// IAgentRuntime implementations are registered by Dev B (e.g. StubAgentRuntime).
// AgentLifecycleService resolves all registered runtimes via IEnumerable<IAgentRuntime>.
builder.Services.AddScoped<AgentLifecycleService>();

// Health checks — T-157
// Base health check services always registered (required by MapHealthChecks).
// RabbitMQ health check only in non-Testing environments (no broker in test).
var healthChecks = builder.Services.AddHealthChecks();
if (!builder.Environment.IsEnvironment("Testing"))
{
    healthChecks.AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "ready" });
}
builder.Services.AddScoped<JobOrchestrationService>(sp =>
    new JobOrchestrationService(
        sp.GetRequiredService<IJobRepository>(),
        sp.GetRequiredService<IWorkTaskRepository>(),
        sp.GetRequiredService<IArtifactRepository>(),
        sp.GetRequiredService<IEventRepository>(),
        sp.GetRequiredService<IWorkspaceRepository>(),
        sp.GetRequiredService<IProjectRepository>(),
        sp.GetRequiredService<IUserCredentialRepository>(),
        sp.GetRequiredService<IWorkspaceService>(),
        sp.GetRequiredService<IArtifactWorkspaceStore>(),
        sp.GetRequiredService<IContainerService>(),
        sp.GetRequiredService<IGitPlatformService>(),
        sp.GetRequiredService<IEncryptionService>(),
        sp.GetRequiredService<IUnitOfWork>(),
        sp.GetRequiredService<ILogger<JobOrchestrationService>>(),
        sp.GetRequiredService<IGovernanceReportRepository>(),
        sp.GetRequiredService<ITaskDependencyRepository>(),
        sp.GetRequiredService<IRealTimeNotifier>(),
        sp.GetRequiredService<ContainerOutputBuffer>(),
        scriptWorkerImage,
        governanceWorkerImage,
        maxGovernanceRetries,
        maxConcurrentTasks));

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
app.UseCors("StewieCors");
app.UseAuthentication(); // Must be before UseAuthorization
app.UseAuthorization();
app.MapControllers();
app.MapHub<StewieHub>("/hubs/stewie");
app.MapHealthChecks("/health");
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
