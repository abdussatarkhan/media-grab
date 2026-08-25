using MediaGrab.Infrastructure.Database;
using MediaGrab.Web.Extensions;
using MediaGrab.Web.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------
// The connection string is never hard-coded. In development it is
// expected to come from `dotnet user-secrets`; in production it comes
// from the ConnectionStrings__DefaultConnection environment variable.
// See README.md for exact setup commands.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "No 'DefaultConnection' connection string was found. Set it via " +
        "'dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...\"' " +
        "(development) or the ConnectionStrings__DefaultConnection environment variable (production).");

builder.Services.AddDbContext<MediaGrabDbContext>(options =>
    options.UseNpgsql(connectionString));

// ---------------------------------------------------------------------
// Identity (authentication foundation)
// ---------------------------------------------------------------------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.SignIn.RequireConfirmedAccount = false; // Phase 3: enable once email sending exists
    })
    .AddEntityFrameworkStores<MediaGrabDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdministrator", policy => policy.RequireRole("Administrator"));
});

// ---------------------------------------------------------------------
// Application services (validators, providers, job orchestration,
// storage, background workers) and rate limiting
// ---------------------------------------------------------------------
builder.Services.AddMediaGrabServices(builder.Configuration);
builder.Services.AddMediaGrabRateLimiting(builder.Configuration);

// ---------------------------------------------------------------------
// MVC / Razor Pages / API
// ---------------------------------------------------------------------
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(); // CSRF protection for forms and, where used, AJAX calls

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "MediaGrab API",
        Version = "v1",
        Description = "URL analysis and authorized-download job API. Development use only - this document is not exposed publicly in production."
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------
// Middleware pipeline
// ---------------------------------------------------------------------
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
else
{
    // Swagger UI is a development-only tool - never exposed in production.
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "MediaGrab API v1"));
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    // Baseline secure headers.
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ---------------------------------------------------------------------
// Startup role seeding
// ---------------------------------------------------------------------
// Ensures the "Administrator" role referenced by the RequireAdministrator
// policy exists. Assigning users to it is a deliberate operational step
// (see README) - never automatic, so no account is ever silently made
// an admin.
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    const string adminRole = "Administrator";
    if (!await roleManager.RoleExistsAsync(adminRole))
    {
        await roleManager.CreateAsync(new IdentityRole(adminRole));
    }
}

app.Run();
