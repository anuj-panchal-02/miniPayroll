using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MiniPayroll.Api.Auth;
using MiniPayroll.Api.Endpoints;
using MiniPayroll.Api.Hosting;
using MiniPayroll.Api.Storage;
using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Identity;
using MiniPayroll.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddScoped<CompanyAdminService>();
builder.Services.AddScoped<CompanySetupService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<SalaryStructureService>();
builder.Services.AddScoped<PayrollCalculationService>();
builder.Services.AddScoped<PayrollInputService>();
builder.Services.AddScoped<LocationCatalogService>();
builder.Services.AddScoped<CompanyLogoUploadCoordinator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "dataprotection-keys")));

var logoStorageOptions = builder.Configuration
    .GetSection("FileStorage")
    .Get<CompanyLogoStorageOptions>() ?? new CompanyLogoStorageOptions();
if (string.IsNullOrWhiteSpace(logoStorageOptions.RootPath)
    || logoStorageOptions.MaxLogoBytes <= 0)
{
    throw new InvalidOperationException(
        "FileStorage:RootPath and a positive FileStorage:MaxLogoBytes are required.");
}

logoStorageOptions.RootPath = Path.GetFullPath(
    logoStorageOptions.RootPath,
    builder.Environment.ContentRootPath);
builder.Services.AddSingleton(Options.Create(logoStorageOptions));
builder.Services.AddSingleton<ICompanyLogoStorage, LocalCompanyLogoStorage>();
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit =
        CompanyLogoUploadLimits.MultipartBodyLengthLimit(
            logoStorageOptions.MaxLogoBytes));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<MiniPayrollDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<MiniPayrollDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = JwtSigningKeyRules.Require(jwt["Key"]);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<IAuthorizationHandler, PasswordChangeCompletedHandler>();

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MiniPayrollDbContext>();
    await db.Database.MigrateAsync();
    await IdentitySeed.EnsureSeededAsync(scope.ServiceProvider);
}

RequestPipeline.Configure(
    app,
    UploadedFilesOptions.Create(logoStorageOptions.RootPath),
    "Frontend");

app.MapGet("/health", () => Results.Ok(new { status = "ok", product = "miniPayroll" }));
app.MapPlatformEndpoints();
app.MapLocationCatalogEndpoints();
app.MapAuthEndpoints();
app.MapCompanyEndpoints();
app.MapCompanySetupEndpoints();
app.MapEmployeeEndpoints();
app.MapSalaryStructureEndpoints();
app.MapPayrollEndpoints();

app.Run();
