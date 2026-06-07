using InCleanHome.ProfileService.Application.Internal.CommandServices;
using InCleanHome.ProfileService.Application.Internal.QueryServices;
using InCleanHome.ProfileService.Configuration;
using InCleanHome.ProfileService.Discovery;
using InCleanHome.ProfileService.Domain.Repositories;
using InCleanHome.ProfileService.Domain.Services;
using InCleanHome.ProfileService.Infrastructure.Persistence;
using InCleanHome.ProfileService.Infrastructure.Persistence.Repositories;
using InCleanHome.ProfileService.Infrastructure.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .MinimumLevel.Information()
    .CreateLogger();

try
{
    Log.Information("Starting InCleanHome Profile Service");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // Infrastructure settings
    var consulAddress = Environment.GetEnvironmentVariable("CONSUL_HTTP_ADDR") ?? "http://consul:8500";
    var serviceName   = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "profile-service";
    var serviceHost   = Environment.GetEnvironmentVariable("SERVICE_HOST") ?? serviceName;
    var servicePort   = int.TryParse(Environment.GetEnvironmentVariable("SERVICE_PORT"), out var p) ? p : 5002;

    var dbConnection = Environment.GetEnvironmentVariable("PROFILE_DB_CONNECTION")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException(
                           "PROFILE_DB_CONNECTION env var is required (PostgreSQL connection string).");

    Log.Information(
        "Identity: name={Name}, host={Host}, port={Port}, consul={Consul}",
        serviceName, serviceHost, servicePort, consulAddress);

    // Load Consul config (fallback: appsettings.json)
    var loadedFromConsul = await ConsulConfigurationLoader.LoadFromConsulAsync(
        builder.Configuration, consulAddress, serviceName);
    if (!loadedFromConsul)
        Log.Warning("Running with LOCAL configuration (appsettings.json).");

    // ASP.NET Core services 
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(opts =>
    {
        opts.SwaggerDoc("v1", new OpenApiInfo
        {
            Title       = "InCleanHome Profile Service",
            Version     = "v1",
            Description = "User profile management microservice"
        });
        opts.EnableAnnotations();
        
        opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "Ingresa el JWT interno generado por IAM. Ejemplo: Bearer eyJhbGciOi...",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

        opts.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // EF Core
    builder.Services.AddDbContext<ProfileDbContext>(opts => opts.UseNpgsql(dbConnection));

    //  Repositories + UoW
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<IClientProfileRepository, ClientProfileRepository>();
    builder.Services.AddScoped<IWorkerProfileRepository, WorkerProfileRepository>();

    //  Application services
    builder.Services.AddScoped<IClientProfileCommandService, ClientProfileCommandService>();
    builder.Services.AddScoped<IClientProfileQueryService, ClientProfileQueryService>();
    builder.Services.AddScoped<IWorkerProfileCommandService, WorkerProfileCommandService>();
    builder.Services.AddScoped<IWorkerProfileQueryService, WorkerProfileQueryService>();

    //  CORS
    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:8080" };
    builder.Services.AddCors(opts =>
    {
        opts.AddDefaultPolicy(p => p.WithOrigins(corsOrigins)
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials());
    });

    // Consul service discovery (opt-in) 
    var registrationOptions = new ConsulRegistrationOptions
    {
        ConsulAddress  = consulAddress,
        ServiceName    = serviceName,
        ServiceId      = $"{serviceName}-{Environment.MachineName}",
        Host           = serviceHost,
        Port           = servicePort,
        Tags           = new[] { "profile", "dotnet" },
        HealthCheckUrl = $"http://{serviceHost}:{servicePort}/health"
    };
    builder.Services.AddSingleton(Options.Create(registrationOptions));
    builder.Services.AddHttpClient<ConsulServiceRegistration>(c => c.Timeout = TimeSpan.FromSeconds(10));
    builder.Services.AddHostedService<ConsulRegistrationHostedService>();

    // Health checks 
    builder.Services.AddHealthChecks().AddDbContextCheck<ProfileDbContext>("profile-db");

    // Build pipeline
    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ProfileDbContext>();
        try
        {
            await db.Database.EnsureCreatedAsync();
            Log.Information("Database schema ensured.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not ensure database schema. Service will exit.");
            throw;
        }
    }

    app.UseSerilogRequestLogging();
    app.UseCors();

    app.MapHealthChecks("/health");

    app.MapGet("/", () => Results.Ok(new
    {
        service      = serviceName,
        status       = "running",
        configSource = loadedFromConsul ? "consul" : "appsettings.json",
        version      = "1.0.0"
    }));

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "InCleanHome Profile Service v1");
        c.RoutePrefix = "swagger";
    });

    // Custom JWT middleware (validates signature + extracts claims)
    app.UseJwtAuth();

    app.MapControllers();

    Log.Information("InCleanHome Profile Service ready on port {Port}", servicePort);
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Profile Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
