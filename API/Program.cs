using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using API.Middleware;
using API.Services;
using Infrastructure.Data;
using Infrastructure.Identity;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Repositories.Implementations;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;

// ── DbContext — per-request TenantId via IHttpContextAccessor ────────────
// Override with TenantId-aware scoped factory so each request gets its own context.
builder.Services.AddScoped<ApplicationDbContext>(sp =>
{
    var accessor = sp.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
    var tenantClaim = accessor?.HttpContext?.User?.FindFirst("tenantId")?.Value;
    var tenantId = Guid.TryParse(tenantClaim, out var tid) ? tid : Guid.Empty;

    var optBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
    optBuilder.UseNpgsql(connStr);
    return new ApplicationDbContext(optBuilder.Options, tenantId);
});

// DbContextOptions needed by Identity internals
builder.Services.AddSingleton(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(connStr).Options);

// ── Identity ──────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(opts =>
{
    opts.Password.RequireDigit = false;
    opts.Password.RequiredLength = 6;
    opts.Password.RequireNonAlphanumeric = false;
    opts.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── JWT ───────────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(opts =>
{
    opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opts.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opts =>
{
    opts.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer           = true,
        ValidIssuer              = builder.Configuration["Jwt:Issuer"],
        ValidateAudience         = true,
        ValidAudience            = builder.Configuration["Jwt:Audience"],
        ValidateLifetime         = true,
        ClockSkew                = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ── Application Services ──────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITenantSetupService, TenantSetupService>();
builder.Services.AddScoped(typeof(IShopService<,,>), typeof(ShopService<,,>));
builder.Services.AddScoped(typeof(ICustomerService<,,>), typeof(CustomerService<,,>));
builder.Services.AddScoped<IMeasurementService, MeasurementService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IKarigarService, KarigarService>();
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped(typeof(IShopExpenseService<,,>), typeof(ShopExpenseService<,,>));
builder.Services.AddScoped(typeof(IStaffSalaryService<,,>), typeof(StaffSalaryService<,,>));
builder.Services.AddScoped(typeof(IStaffAttendanceService<,,>), typeof(StaffAttendanceService<,,>));

// ── Unit of Work ──────────────────────────────────────────────────────────
builder.Services.AddScoped<Application.Interfaces.IUnitOfWork, Infrastructure.Data.UnitOfWork>();

// ── Repositories ──────────────────────────────────────────────────────────
builder.Services.AddScoped<IShopRepository, ShopRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IMeasurementRepository, MeasurementRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IFinanceRepository, FinanceRepository>();
builder.Services.AddScoped<IShopExpenseRepository, ShopExpenseRepository>();
builder.Services.AddScoped<IStaffSalaryRepository, StaffSalaryRepository>();
builder.Services.AddScoped<IStaffAttendanceRepository, StaffAttendanceRepository>();

// ── AutoMapper ────────────────────────────────────────────────────────────
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// ── Controllers + Swagger ─────────────────────────────────────────────────
builder.Services.AddControllers();
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
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter JWT token"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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

// ── CORS ──────────────────────────────────────────────────────────────────
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// ── Migrate + Seed on startup ─────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var optBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
    optBuilder.UseNpgsql(connStr);
    var db = new ApplicationDbContext(optBuilder.Options, Guid.Empty);

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var tenantSetup = scope.ServiceProvider.GetRequiredService<ITenantSetupService>();
    await db.Database.MigrateAsync();
    await DbInitializer.SeedAsync(roleManager, userManager, db, tenantSetup);
}

// ── Middleware pipeline ───────────────────────────────────────────────────
app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Digital Darzi API v1"));
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
