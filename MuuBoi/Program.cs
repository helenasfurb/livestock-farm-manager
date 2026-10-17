using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MuuBoi.Api.Middleware;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.Options;
using MuuBoi.Application.Services;
using MuuBoi.Infrastructure.Data;
using MuuBoi.Infrastructure.Repositories;
using MuuBoi.Infrastructure.Services;
using MuuBoi.Domain.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentityCore<ApplicationUser>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key não configurada.");

builder.Services.Configure<PortalOptions>(builder.Configuration.GetSection("Portal"));

var portalJwtKey = builder.Configuration["Portal:JwtKey"];
var portalSigningKey = string.IsNullOrEmpty(portalJwtKey)
    ? RandomNumberGenerator.GetBytes(64)
    : Encoding.UTF8.GetBytes(portalJwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var securityStamp = context.Principal?.FindFirstValue("security_stamp");

            if (userId == null || securityStamp == null)
            {
                context.Fail("Sessão inválida.");
                return;
            }

            var sessionValidator = context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();
            if (!await sessionValidator.IsValidAsync(userId, securityStamp))
                context.Fail("Sessão inválida.");
        }
    };
})
.AddJwtBearer(PortalAuthDefaults.Scheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = true,
        ValidAudience = PortalAuthDefaults.Audience,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(portalSigningKey),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PortalAuthDefaults.Policy, policy => policy
        .AddAuthenticationSchemes(PortalAuthDefaults.Scheme)
        .RequireAuthenticatedUser()
        .RequireRole(PortalAuthDefaults.Role));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Muitas tentativas. Tente novamente mais tarde." }, ct);

    options.AddPolicy(PortalAuthDefaults.LoginRateLimit, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ResolveClientIp(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
});

builder.Services.AddScoped<IPortalAuthService, PortalAuthService>();

builder.Services.AddScoped<IAnimalRepository, AnimalRepository>();
builder.Services.AddScoped<IAnimalService, AnimalService>();

builder.Services.AddScoped<IWeightRecordRepository, WeightRecordRepository>();
builder.Services.AddScoped<IWeightRecordService, WeightRecordService>();

builder.Services.AddScoped<IVaccineRepository, VaccineRepository>();
builder.Services.AddScoped<IVaccineService, VaccineService>();

builder.Services.AddScoped<IMedicationRepository, MedicationRepository>();
builder.Services.AddScoped<IMedicationService, MedicationService>();

builder.Services.AddScoped<IAnimalMedicationRepository, AnimalMedicationRepository>();
builder.Services.AddScoped<IAnimalMedicationService, AnimalMedicationService>();

builder.Services.AddScoped<IBodyConditionRecordRepository, BodyConditionRecordRepository>();
builder.Services.AddScoped<IBodyConditionRecordService, BodyConditionRecordService>();

builder.Services.AddScoped<IAnimalExitRecordRepository, AnimalExitRecordRepository>();

builder.Services.AddScoped<ISemenSampleRepository, SemenSampleRepository>();
builder.Services.AddScoped<ISemenSampleMovementRepository, SemenSampleMovementRepository>();
builder.Services.AddScoped<ISemenSampleMovementService, SemenSampleMovementService>();
builder.Services.AddScoped<ISemenSampleService, SemenSampleService>();

builder.Services.AddScoped<IBreedingEventRepository, BreedingEventRepository>();
builder.Services.AddScoped<IBreedingEventService, BreedingEventService>();

builder.Services.AddScoped<IAnimalPregnancyRepository, AnimalPregnancyRepository>();
builder.Services.AddScoped<IAnimalPregnancyService, AnimalPregnancyService>();

builder.Services.AddScoped<IAnimalCalvingRepository, AnimalCalvingRepository>();
builder.Services.AddScoped<IAnimalCalvingService, AnimalCalvingService>();

builder.Services.AddScoped<IMilkProductionRepository, MilkProductionRepository>();
builder.Services.AddScoped<IMilkProductionService, MilkProductionService>();

builder.Services.AddScoped<ILactationRepository, LactationRepository>();
builder.Services.AddScoped<ILactationService, LactationService>();

builder.Services.AddScoped<IVaccinationEventRepository, VaccinationEventRepository>();
builder.Services.AddScoped<IVaccinationEventService, VaccinationEventService>();

builder.Services.AddScoped<IHealthCaseRepository, HealthCaseRepository>();
builder.Services.AddScoped<IHealthCaseService, HealthCaseService>();

builder.Services.AddScoped<IStockReferenceRepository, StockReferenceRepository>();
builder.Services.AddScoped<IStockItemRepository, StockItemRepository>();
builder.Services.AddScoped<IStockMovementRepository, StockMovementRepository>();
builder.Services.AddScoped<IStockReferenceService, StockReferenceService>();
builder.Services.AddScoped<IStockMovementService, StockMovementService>();
builder.Services.AddScoped<IStockItemService, StockItemService>();

builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAccountService, AccountService>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IUsageTracker, UsageTracker>();
builder.Services.AddScoped<IUsageRepository, UsageRepository>();
builder.Services.AddScoped<IUsageService, UsageService>();
builder.Services.AddHostedService<UsageFlushService>();

builder.Services.AddScoped<IPortalRepository, PortalRepository>();
builder.Services.AddScoped<IPortalMetricsService, PortalMetricsService>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISessionValidator, SessionValidator>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddAutoMapper(typeof(Program));

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Insira o token JWT. Exemplo: Bearer {seu_token}"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<RequestLoggingMiddleware>();

if (!app.Environment.IsEnvironment("Docker"))
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseMiddleware<UsageTrackingMiddleware>();

app.UseMiddleware<ExceptionMiddleware>();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
}

app.Run();

static string ResolveClientIp(HttpContext context)
{
    var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
    if (!string.IsNullOrEmpty(forwarded))
        return forwarded.Split(',').Last().Trim();

    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
