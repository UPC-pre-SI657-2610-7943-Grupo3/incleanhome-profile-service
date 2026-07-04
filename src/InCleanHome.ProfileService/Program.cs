using InCleanHome.ProfileService.Application.Internal.CommandServices;
using InCleanHome.ProfileService.Application.Internal.QueryServices;
using InCleanHome.ProfileService.Configuration;
using InCleanHome.ProfileService.Discovery;
using InCleanHome.ProfileService.Domain.Repositories;
using InCleanHome.ProfileService.Domain.Services;
using InCleanHome.ProfileService.Infrastructure.Messaging.Consumers;
using InCleanHome.ProfileService.Infrastructure.Persistence;
using InCleanHome.ProfileService.Infrastructure.Persistence.Repositories;
using InCleanHome.ProfileService.Infrastructure.Pipeline;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks; 
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

    var consulAddress = Environment.GetEnvironmentVariable("CONSUL_HTTP_ADDR") ?? "http://consul:8500";
    var serviceName   = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "profile-service";
    var serviceHost   = Environment.GetEnvironmentVariable("SERVICE_HOST") ?? serviceName;
    var servicePort   = int.TryParse(Environment.GetEnvironmentVariable("SERVICE_PORT"), out var p) ? p : 5002;

    var dbConnection = Environment.GetEnvironmentVariable("PROFILE_DB_CONNECTION")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException(
                           "PROFILE_DB_CONNECTION env var is required (PostgreSQL connection string).");

    var rabbitMqUrl = Environment.GetEnvironmentVariable("RABBITMQ_URL") ?? string.Empty;
    var rabbitMqEnabled = !string.IsNullOrWhiteSpace(rabbitMqUrl)
                         && !rabbitMqUrl.Contains("placeholder", StringComparison.OrdinalIgnoreCase);

    Log.Information(
        "Identity: name={Name}, host={Host}, port={Port}, broker={Broker}",
        serviceName, serviceHost, servicePort,
        rabbitMqEnabled ? "configured" : "DISABLED (placeholder)");

    var loadedFromConsul = await ConsulConfigurationLoader.LoadFromConsulAsync(
        builder.Configuration, consulAddress, serviceName);
    if (!loadedFromConsul)
        Log.Warning("Running with LOCAL configuration (appsettings.json).");

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(opts =>
    {
        opts.SwaggerDoc("v1", new OpenApiInfo
        {
            Title       = "InCleanHome Profile Service",
            Version     = "v1",
            Description = "Profile management — ClientProfile + WorkerProfile"
        });
        opts.EnableAnnotations();
        opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header. Example: 'Bearer eyJhbGciOi...'",
            Name        = "Authorization",
            In          = ParameterLocation.Header,
            Type        = SecuritySchemeType.ApiKey,
            Scheme      = "Bearer"
        });
        opts.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddDbContext<ProfileDbContext>(opts => opts.UseNpgsql(dbConnection));

    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<IClientProfileRepository, ClientProfileRepository>();
    builder.Services.AddScoped<IWorkerProfileRepository, WorkerProfileRepository>();

    builder.Services.AddScoped<IClientProfileCommandService, ClientProfileCommandService>();
    builder.Services.AddScoped<IClientProfileQueryService, ClientProfileQueryService>();
    builder.Services.AddScoped<IWorkerProfileCommandService, WorkerProfileCommandService>();
    builder.Services.AddScoped<IWorkerProfileQueryService, WorkerProfileQueryService>();
    
    //  MassTransit + RabbitMQ
    builder.Services.AddMassTransit(x =>
    {
        // Consumers: Profile reacts to events from IAM and Reviews.
        x.AddConsumer<ReviewSubmittedConsumer>();
        x.AddConsumer<UserDeletedConsumer>();

        if (rabbitMqEnabled)
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(rabbitMqUrl));
                cfg.ConfigureEndpoints(context);
            });
        }
        else
        {
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        }
    });

    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:8080" };
    builder.Services.AddCors(opts =>
    {
        opts.AddDefaultPolicy(p => p.WithOrigins(corsOrigins)
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials());
    });

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

    builder.Services.AddHealthChecks().AddDbContextCheck<ProfileDbContext>("profile-db");

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

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = check => check.Name != "masstransit-bus"
    });
    app.MapGet("/", () => Results.Ok(new
    {
        service      = serviceName,
        status       = "running",
        configSource = loadedFromConsul ? "consul" : "appsettings.json",
        broker       = rabbitMqEnabled ? "configured" : "disabled",
        version      = "1.0.0"
    }));

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "InCleanHome Profile Service v1");
        c.RoutePrefix = "swagger";
    });

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
