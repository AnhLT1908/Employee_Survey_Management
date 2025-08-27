using System.Text;
using System.Threading.Tasks;
using HRTestInfrastructure.Data;
using HRTestInfrastructure.Identity;
using HRTestWeb.Options;
using HRTestWeb.Services.Email;
using HRTestWeb.Services.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ===== Roles seed =====
var roleNames = new[] { "Admin", "Dev", "Tester", "BA", "SA", "QAQC", "Designer", "HR", "PM" };

// ===== DB =====
builder.Services.AddDbContext<HRTestDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ===== Identity (cookie) =====
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

// Cookie: API trả status thay vì redirect
builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/Account/Login";
    opt.AccessDeniedPath = "/Account/AccessDenied";
    opt.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    opt.Cookie.SameSite = SameSiteMode.None;

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

// ===== MVC + global filters =====
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<HRTestWeb.Filters.AuditActionFilter>();
});

// ===== Email (SMTP) =====
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Email:Smtp"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

// ===== Filters/Middlewares DI =====
builder.Services.AddSingleton<HRTestWeb.Services.Logging.RequestLogFileWriter>();
builder.Services.AddScoped<HRTestWeb.Filters.AuditActionFilter>();

// ===== JWT =====
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
          ?? throw new InvalidOperationException("Missing Jwt section in appsettings.json");

builder.Services.AddMemoryCache();

builder.Services.AddScoped<IAppSettingsService, AppSettingsService>();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Email:Smtp"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

// Session 30'
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromMinutes(30);
    opt.Cookie.HttpOnly = true;
    opt.Cookie.IsEssential = true;
});


// Authentication: policy scheme tự chọn Cookie cho web, JWT cho API/Bearer
var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "AppAuth";
    options.DefaultChallengeScheme = "AppAuth";
})
.AddPolicyScheme("AppAuth", "Cookie or JWT", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var hasBearer = context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
        var isApi = context.Request.Path.StartsWithSegments("/api");
        var hasJwtCookie = context.Request.Cookies.ContainsKey("auth_token");
        return (hasBearer || hasJwtCookie || isApi)
            ? JwtBearerDefaults.AuthenticationScheme
            : IdentityConstants.ApplicationScheme; // cookie của Identity
    };
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero   // hết hạn là 401 ngay
    };

    // Lấy token từ cookie "auth_token" nếu không có header
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            if (string.IsNullOrEmpty(ctx.Token) &&
                ctx.Request.Cookies.TryGetValue("auth_token", out var t))
            {
                ctx.Token = t;
            }
            return Task.CompletedTask;
        }
    };
});

// ===== Google SSO (nếu có cấu hình) =====
var google = builder.Configuration.GetSection("Authentication:Google");
var googleId = google["ClientId"];
var googleSecret = google["ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleId) && !string.IsNullOrWhiteSpace(googleSecret))
{
    authBuilder.AddGoogle("Google", opts =>
    {
        opts.ClientId = googleId!;
        opts.ClientSecret = googleSecret!;
        opts.CallbackPath = "/signin-google";
        opts.SaveTokens = true;
    });
}

var app = builder.Build();


//using (var scope = app.Services.CreateScope())
//{
//    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

//var email = "letienanh1908@gmail.com";
//    var user = await userManager.FindByEmailAsync(email);

//    if (user == null)
//    {
//        user = new ApplicationUser
//        {
//            UserName = email,
//            Email = email,
//            EmailConfirmed = true,
//            FullName = "Tien Anh"
//        };

//        var create = await userManager.CreateAsync(user, "Leanh2003");
//        if (!create.Succeeded)
//        {
//            throw new Exception("Seed user creation failed: " +
//                string.Join("; ", create.Errors.Select(e => e.Description)));
//        }

//        // (Tuỳ chọn) gán role mặc định
//        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
//        const string defaultRole = "Dev";
//        if (!await roleManager.RoleExistsAsync(defaultRole))
//            await roleManager.CreateAsync(new IdentityRole(defaultRole));
//        await userManager.AddToRoleAsync(user, defaultRole);
//    }


//}

// ===== Auto-migrate (dev) =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HRTestDbContext>();
    db.Database.Migrate();
}

// ===== Error handling =====
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ===== Forwarded headers (ngrok/proxy) =====
var fwd = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

app.UseHttpsRedirection();
app.UseStaticFiles();

// ===== custom middlewares =====
app.UseMiddleware<HRTestWeb.Middlewares.RequestLoggingMiddleware>();
app.UseMiddleware<HRTestWeb.Middlewares.BlockRuleMiddleware>();

app.UseRouting();
app.UseSession();
app.UseAuthentication();   // << phải trước Authorization
app.UseAuthorization();

// ===== seed roles & add Admin to admin@fint.com =====
using (var scope = app.Services.CreateScope())
{
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var r in roleNames)
        if (!await roleMgr.RoleExistsAsync(r))
            await roleMgr.CreateAsync(new IdentityRole(r));

    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var admin = await userMgr.FindByEmailAsync("admin@fint.com");
    if (admin != null && !await userMgr.IsInRoleAsync(admin, "Admin"))
        await userMgr.AddToRoleAsync(admin, "Admin");
}

// ===== endpoints =====
app.MapControllers();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapRazorPages();

app.Run();
