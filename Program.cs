using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using tran_nguyen_vi_khang_api.Data;
using tran_nguyen_vi_khang_api.Services;
using tran_nguyen_vi_khang_api.Services.Interfaces;

// 1. Locate and load .env file without overriding existing system/process environment variables
LoadEnvironmentVariables();

var builder = WebApplication.CreateBuilder(args);

// 2. Validate mandatory configurations early before building the container
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException(
        "Critical configuration missing: 'Jwt:Secret' (or 'Jwt__Secret') must be configured in .env or environment variables and must be at least 32 bytes (256 bits).");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
if (string.IsNullOrWhiteSpace(jwtIssuer))
{
    throw new InvalidOperationException(
        "Critical configuration missing: 'Jwt:Issuer' (or 'Jwt__Issuer') must be configured in .env or environment variables.");
}

var jwtAudience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "Critical configuration missing: 'Jwt:Audience' (or 'Jwt__Audience') must be configured in .env or environment variables.");
}

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
if (string.IsNullOrWhiteSpace(googleClientId))
{
    throw new InvalidOperationException(
        "Critical configuration missing: 'Authentication:Google:ClientId' (or 'Authentication__Google__ClientId') must be configured in .env or environment variables.");
}

// 3. Resolve SQL Server connection string using base settings and MSSQL_SA_PASSWORD
var baseConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost,1434;Database=TranNguyenViKhangDB;User Id=sa;TrustServerCertificate=True;Encrypt=True";

var sqlBuilder = new SqlConnectionStringBuilder(baseConnectionString);
if (string.IsNullOrWhiteSpace(sqlBuilder.Password))
{
    var saPassword = builder.Configuration["MSSQL_SA_PASSWORD"];
    if (!string.IsNullOrWhiteSpace(saPassword))
    {
        sqlBuilder.Password = saPassword;
    }
}

if (!sqlBuilder.IntegratedSecurity && sqlBuilder.Authentication == SqlAuthenticationMethod.NotSpecified && string.IsNullOrWhiteSpace(sqlBuilder.Password))
{
    throw new InvalidOperationException(
        "Critical configuration missing: SQL Server password is not set. Ensure 'MSSQL_SA_PASSWORD' is configured in .env or 'ConnectionStrings:DefaultConnection' includes a valid password.");
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks();

// Configure Forwarded Headers for reverse proxy (Caddy, Nginx)
// Note: In container bridge networks, proxy IP is dynamically assigned.
// Setting ForwardLimit=2 prevents header spoofing across nested hops.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = 2;
});

// Configure CORS
// Native mobile apps (Android/iOS) communicate via direct TCP/HTTP sockets and do not enforce browser CORS.
// CORS is configured here to support Flutter Web, Swagger UI, or administrative web portals.
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        if (configuredOrigins.Length > 0)
        {
            policy.WithOrigins(configuredOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            // Default policy for mobile API calls and local development
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

// Register application business and authentication services
builder.Services.AddScoped<IEmailSenderService, EmailSenderService>();
builder.Services.AddScoped<IChildService, ChildService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IMonitoringRuleService, MonitoringRuleService>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<ISafetyAdvisorService, SafetyAdvisorService>();
builder.Services.AddScoped<IActivityService, ActivityService>();

// Configure JWT Bearer Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Configure Swagger/OpenAPI documentation with JWT Bearer support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Parental Control API",
        Version = "v1",
        Description = "ASP.NET Core Web API for Parental Control application with Google Sign-In and JWT Authentication."
    });

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Add JWT Bearer Security Definition for Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your application JWT token in the format: Bearer {your_token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(sqlBuilder.ConnectionString));

var app = builder.Build();

// 1. Process X-Forwarded-For and X-Forwarded-Proto from reverse proxy (Caddy)
app.UseForwardedHeaders();

// 2. Safe incremental database migrations on startup with resilient retry loop
// Opt-in via APPLY_MIGRATIONS_ON_STARTUP=true in environment variables
if (builder.Configuration.GetValue<bool>("APPLY_MIGRATIONS_ON_STARTUP", false))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    const int maxMigrationAttempts = 8;
    const int delaySeconds = 3;

    for (int attempt = 1; attempt <= maxMigrationAttempts; attempt++)
    {
        try
        {
            logger.LogInformation("Applying EF Core database migrations (attempt {Attempt}/{MaxAttempts})...", attempt, maxMigrationAttempts);
            dbContext.Database.Migrate();
            logger.LogInformation("Database migrations applied and verified successfully.");
            break;
        }
        catch (SqlException sqlEx) when (attempt < maxMigrationAttempts)
        {
            logger.LogWarning(sqlEx, "SQL Server is starting up or temporarily unavailable. Retrying in {Delay}s...", delaySeconds);
            Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error executing database migrations on startup.");
            throw;
        }
    }
}

// 3. Configure Swagger UI (in Development, or when ENABLE_SWAGGER=true in Production)
var enableSwagger = app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("ENABLE_SWAGGER", false);
if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Parental Control API v1");
    });
}

// 4. CORS Middleware
app.UseCors("DefaultCorsPolicy");

// 5. HTTPS Redirection (controlled via ENABLE_HTTPS_REDIRECTION flag; Caddy handles SSL termination)
if (!app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("ENABLE_HTTPS_REDIRECTION", false))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

// 6. Built-in Health Check endpoint for Docker and monitoring
app.MapHealthChecks("/health");

app.MapControllers();

app.Run();

// Helper methods to locate and load .env file
static void LoadEnvironmentVariables()
{
    var envPath = FindEnvFile();
    if (envPath != null && File.Exists(envPath))
    {
        // NoClobber ensures System/OS environment variables take higher precedence
        DotNetEnv.Env.NoClobber().Load(envPath);
    }
}

static string? FindEnvFile()
{
    // 1. Check current working directory
    var currentDirEnv = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (File.Exists(currentDirEnv)) return currentDirEnv;

    // 2. Check subdirectory if launched from solution root
    var subDirEnv = Path.Combine(Directory.GetCurrentDirectory(), "tran_nguyen_vi_khang_api", ".env");
    if (File.Exists(subDirEnv)) return subDirEnv;

    // 3. Traverse up from AppContext.BaseDirectory
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        var candidate = Path.Combine(dir.FullName, ".env");
        if (File.Exists(candidate)) return candidate;

        var candidateSub = Path.Combine(dir.FullName, "tran_nguyen_vi_khang_api", ".env");
        if (File.Exists(candidateSub)) return candidateSub;

        dir = dir.Parent;
    }

    return null;
}
