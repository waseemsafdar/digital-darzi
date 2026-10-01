using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using API.Authorization;
using API.Health;
using API.Json;
using API.Middleware;
using API.Services;
using API.Validation;
using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Mappings;
using Application.Validation;
using FluentValidation;
using Infrastructure.Data;
using Infrastructure.Identity;
using Infrastructure.Notifications;
using Infrastructure.Services;
using Infrastructure.Services.Orders;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Repositories.Implementations;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ── Logging ──────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, services, logger) => logger
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// ── Configuration that must exist ────────────────────────────────────────
var connStr = config.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connStr))
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. Set it with user-secrets or the ConnectionStrings__DefaultConnection environment variable.");
var jwtKey = config["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key must be configured and at least 32 bytes long (use user-secrets or the Jwt__Key environment variable).");

// ── Database: one tenant-scoped context per request ───────────────────────
var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(connStr, o => o.CommandTimeout(config.GetValue("Database:CommandTimeoutSeconds", 30)))
    .Options;
builder.Services.AddSingleton(dbOptions);
builder.Services.AddSingleton<ISystemDbContextFactory, SystemDbContextFactory>();
builder.Services.AddScoped<ApplicationDbContext>(sp =>
{
    var user = sp.GetService<IHttpContextAccessor>()?.HttpContext?.User;
    Guid Claim(string type) => Guid.TryParse(user?.FindFirst(type)?.Value, out var id) ? id : Guid.Empty;
    var clock = sp.GetRequiredService<TimeProvider>();
    return new ApplicationDbContext(dbOptions, Claim("tenantId"), Claim("shopId"), Claim(ClaimTypes.NameIdentifier))
    {
        UtcNow = () => clock.GetUtcNow().UtcDateTime
    };
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

// ── Identity ─────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(opts =>
{
    opts.Password.RequireDigit = false;
    opts.Password.RequiredLength = 8;
    opts.Password.RequireNonAlphanumeric = false;
    opts.Password.RequireUppercase = false;
    opts.Lockout.AllowedForNewUsers = true;
    opts.Lockout.MaxFailedAccessAttempts = AuthService.MaxFailedAttempts;
    opts.Lockout.DefaultLockoutTimeSpan = AuthService.LockoutDuration;
    opts.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── JWT ──────────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(opts =>
{
    opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opts.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opts =>
{
    opts.MapInboundClaims = true;
    opts.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer           = true,
        ValidIssuer              = config["Jwt:Issuer"],
        ValidateAudience         = true,
        ValidAudience            = config["Jwt:Audience"],
        ValidateLifetime         = true,
        ClockSkew                = TimeSpan.FromSeconds(30)
    };
});

// ── Authorization: named permissions ("perm:orders.create") ───────────────
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// ── Application services ─────────────────────────────────────────────────
builder.Services.AddSingleton<MappingRegistry>();
builder.Services.AddSingleton<IObjectMapper, ObjectMapper>();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IShopSettingsCache, ShopSettingsCache>();
builder.Services.AddScoped<IBusinessCalendar, BusinessCalendar>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IPlanLimitService, PlanLimitService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITenantSetupService, TenantSetupService>();
builder.Services.AddScoped(typeof(IShopService<,,>), typeof(ShopService<,,>));
builder.Services.AddScoped(typeof(ICustomerService<,,>), typeof(CustomerService<,,>));
builder.Services.AddScoped<IMeasurementService, MeasurementService>();
builder.Services.AddScoped<OrderCore>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();
builder.Services.AddScoped<IOrderPaymentService, OrderPaymentService>();
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAdminProvisioningService, AdminProvisioningService>();
builder.Services.AddScoped<IKarigarService, KarigarService>();
builder.Services.AddScoped<IKarigarLedgerService, KarigarLedgerService>();
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IGarmentAttributeService, GarmentAttributeService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ICashService, CashService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IWorkBoardService, WorkBoardService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped(typeof(IShopExpenseService<,,>), typeof(ShopExpenseService<,,>));
builder.Services.AddScoped(typeof(IStaffSalaryService<,,>), typeof(StaffSalaryService<,,>));
builder.Services.AddScoped(typeof(IStaffAttendanceService<,,>), typeof(StaffAttendanceService<,,>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ── Repositories ─────────────────────────────────────────────────────────
builder.Services.AddScoped<IShopRepository, ShopRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IMeasurementRepository, MeasurementRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IFinanceRepository, FinanceRepository>();
builder.Services.AddScoped<IShopExpenseRepository, ShopExpenseRepository>();
builder.Services.AddScoped<IStaffSalaryRepository, StaffSalaryRepository>();
builder.Services.AddScoped<IStaffAttendanceRepository, StaffAttendanceRepository>();

// ── Files ────────────────────────────────────────────────────────────────
builder.Services.Configure<FileStorageOptions>(config.GetSection(FileStorageOptions.Section));
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

// ── Notifications (outbox + background delivery) ─────────────────────────
builder.Services.Configure<NotificationOptions>(config.GetSection(NotificationOptions.Section));
builder.Services.AddHttpClient<WhatsAppCloudApiSender>(c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient<HttpSmsSender>(c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddTransient<INotificationSender>(sp => sp.GetRequiredService<WhatsAppCloudApiSender>());
builder.Services.AddTransient<INotificationSender>(sp => sp.GetRequiredService<HttpSmsSender>());
builder.Services.AddScoped<NotificationSenderResolver>();
builder.Services.AddSingleton<NotificationDispatcher>();
builder.Services.AddSingleton<ReminderJob>();
if (config.GetValue("Notifications:DispatcherEnabled", true))
    builder.Services.AddHostedService(sp => sp.GetRequiredService<NotificationDispatcher>());
if (config.GetValue("Notifications:RemindersEnabled", true))
    builder.Services.AddHostedService(sp => sp.GetRequiredService<ReminderJob>());

// ── Controllers, validation, JSON ────────────────────────────────────────
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderValidator>();
builder.Services.AddScoped<ValidationFilter>();
builder.Services.AddControllers(opts =>
    {
        opts.Filters.AddService<ValidationFilter>();
        opts.ModelBinderProviders.Insert(0, new UtcDateTimeModelBinderProvider());
    })
    .ConfigureApiBehaviorOptions(opts => opts.SuppressModelStateInvalidFilter = true)   // ValidationFilter returns the standard envelope
    .AddJsonOptions(opts =>
    {
        // Npgsql only accepts Kind=Utc for 'timestamp with time zone'. Normalise on the way in.
        opts.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        opts.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
    });

// ── Rate limiting: sign-in endpoints ─────────────────────────────────────
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opts.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = config.GetValue("RateLimiting:AuthPermitPerMinute", 20),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    opts.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(
            ApiResponse<object>.Fail("Too many attempts. Wait a minute and try again.", 429),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), ct);
    };
});

// ── Health ───────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

// ── Swagger ──────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Digital Darzi API",
        Version     = "v1",
        Description = "Multi-tenant tailor shop management system"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header, Description = "Enter JWT token"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
    c.CustomSchemaIds(t => t.FullName?.Replace("+", "."));
    var xml = Path.Combine(AppContext.BaseDirectory, "API.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

// ── CORS: explicit origins in production ─────────────────────────────────
var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(opts => opts.AddDefaultPolicy(p =>
{
    if (allowedOrigins.Length > 0) p.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
    else p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
}));

var app = builder.Build();

// ── Migrations: an explicit step, not a side effect of every start ───────
var migrateOnly = args.Contains("--migrate");
if (migrateOnly || config.GetValue("Database:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var factory = scope.ServiceProvider.GetRequiredService<ISystemDbContextFactory>();
    await using (var migrationDb = factory.Create())
        await migrationDb.Database.MigrateAsync();

    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DbInitializer.SeedAsync(
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        db,
        scope.ServiceProvider.GetRequiredService<ITenantSetupService>(),
        new DbInitializer.AdminSeed(config["Seed:AdminEmail"], config["Seed:AdminPassword"]),
        app.Logger);

    if (migrateOnly)
    {
        app.Logger.LogInformation("Database migrated and seeded; exiting (--migrate).");
        return;
    }
}
else if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<ISystemDbContextFactory>().Create();
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count > 0)
        app.Logger.LogWarning("{Count} database migration(s) are pending: {Migrations}. Run the API with --migrate.", pending.Count, string.Join(", ", pending));
}

// ── Middleware pipeline ───────────────────────────────────────────────────
app.UseSerilogRequestLogging();
app.UseGlobalExceptionHandler();

if (config.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Digital Darzi API v1"));
}

app.UseCors();
if (config.GetValue("UseHttpsRedirection", true)) app.UseHttpsRedirection();
app.UseAuthentication();
app.Use(async (ctx, next) =>
{
    // Tenant and user on every log line written while handling the request.
    using (LogContext.PushProperty("TenantId", ctx.User.FindFirst("tenantId")?.Value))
    using (LogContext.PushProperty("ShopId", ctx.User.FindFirst("shopId")?.Value))
    using (LogContext.PushProperty("UserId", ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value))
        await next();
});
app.UseRateLimiter();
app.UseAuthorization();
app.UseSubscriptionCheck();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.Run();

/// <summary>Entry point marker for WebApplicationFactory in the integration tests.</summary>
public partial class Program { }
