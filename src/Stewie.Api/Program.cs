using FluentMigrator.Runner;
using Stewie.Application.Interfaces;
using Stewie.Application.Services;
using Stewie.Infrastructure.Persistence;
using Stewie.Infrastructure.Repositories;
using Stewie.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var connectionString = builder.Configuration.GetConnectionString("Stewie")
    ?? throw new InvalidOperationException("Connection string 'Stewie' is required.");
var workspaceRoot = builder.Configuration.GetValue<string>("Stewie:WorkspaceRoot") ?? "./workspaces";
var dockerImageName = builder.Configuration.GetValue<string>("Stewie:DockerImageName") ?? "stewie-dummy-worker";

// Controllers & OpenAPI 
builder.Services.AddControllers();
builder.Services.AddOpenApi();

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

// Services
builder.Services.AddSingleton<IWorkspaceService>(sp =>
    new WorkspaceService(workspaceRoot, sp.GetRequiredService<ILogger<WorkspaceService>>()));
builder.Services.AddSingleton<IContainerService>(sp =>
    new DockerContainerService(dockerImageName, sp.GetRequiredService<ILogger<DockerContainerService>>()));
builder.Services.AddScoped<RunOrchestrationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
