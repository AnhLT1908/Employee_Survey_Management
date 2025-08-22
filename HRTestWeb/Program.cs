using System.Threading.Tasks;
using HRTestInfrastructure.Data;
using HRTestInfrastructure.Identity;
using HRTestWeb.Services.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// DB
builder.Services.AddDbContext<HRTestDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<HRTestDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.AddControllersWithViews();

// Cookie (API trả JSON status code)
builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/Account/Login";
    opt.AccessDeniedPath = "/Account/AccessDenied";
    opt.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    opt.Cookie.SameSite = SameSiteMode.None; // cần cho SSO qua proxy

    opt.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    opt.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

// External providers (Google)
var auth = builder.Services.AddAuthentication();

var google = builder.Configuration.GetSection("Authentication:Google");
var googleId = google["ClientId"];
var googleSecret = google["ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleId) && !string.IsNullOrWhiteSpace(googleSecret))
{
    auth.AddGoogle("Google", opts =>
    {
        opts.ClientId = googleId;
        opts.ClientSecret = googleSecret;
        opts.CallbackPath = "/signin-google";    // trùng Redirect URI trên Google
        opts.SaveTokens = true;
    });
}

// Email (SMTP)
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Email:Smtp"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    var email = "admin@fint.com";
    var user = await userManager.FindByEmailAsync(email);

    if (user == null)
    {
        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = "Admin System"
        };

        var create = await userManager.CreateAsync(user, "Leanh2003");
        if (!create.Succeeded)
        {
            throw new Exception("Seed user creation failed: " +
                string.Join("; ", create.Errors.Select(e => e.Description)));
        }

        // (Tuỳ chọn) gán role mặc định
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        const string defaultRole = "Admin";
        if (!await roleManager.RoleExistsAsync(defaultRole))
            await roleManager.CreateAsync(new IdentityRole(defaultRole));
        await userManager.AddToRoleAsync(user, defaultRole);
    }
}

// (Tuỳ chọn dev) tự migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HRTestDbContext>();
    db.Database.Migrate();
}

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Quan trọng cho ngrok: tôn trọng X-Forwarded-Proto/Host để tạo absolute URL đúng domain ngrok
var fwd = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapRazorPages();

app.Run();
