using HRTestInfrastructure.Data;
using HRTestInfrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using HRTestWeb.Services.Email;


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

// Cookie: chưa đăng nhập sẽ bị chuyển về /Account/Login
builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/Account/Login";
    opt.AccessDeniedPath = "/Account/Login";
});

builder.Services.AddControllersWithViews();

// -------- External Login Providers (bật theo cấu hình trong appsettings) --------
var auth = builder.Services.AddAuthentication();

var google = builder.Configuration.GetSection("Authentication:Google");
if (!string.IsNullOrWhiteSpace(google["ClientId"]))
{
    auth.AddGoogle(opts =>
    {
        opts.ClientId = google["ClientId"];
        opts.ClientSecret = google["ClientSecret"];
    });
}

var msft = builder.Configuration.GetSection("Authentication:Microsoft");
if (!string.IsNullOrWhiteSpace(msft["ClientId"]))
{
    auth.AddMicrosoftAccount(opts =>
    {
        opts.ClientId = msft["ClientId"];
        opts.ClientSecret = msft["ClientSecret"];
    });
}

var azure = builder.Configuration.GetSection("Authentication:AzureAd");
if (!string.IsNullOrWhiteSpace(azure["ClientId"]) && !string.IsNullOrWhiteSpace(azure["TenantId"]))
{
    auth.AddOpenIdConnect("AzureAd", opts =>
    {
        opts.Authority = $"https://login.microsoftonline.com/{azure["TenantId"]}/v2.0";
        opts.ClientId = azure["ClientId"];
        opts.ClientSecret = azure["ClientSecret"];
        opts.ResponseType = "code";
        opts.CallbackPath = "/signin-oidc-azuread";
        opts.SaveTokens = true;
        opts.Scope.Add("email");
        opts.Scope.Add("profile");
    });
}

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Email:Smtp"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();


// -------------------------------------------------------------------------------

var app = builder.Build();


// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Route mặc định → Home/Index (đăng nhập xong từ AccountController đã RedirectToAction("Index","Home"))
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages(); // nếu dùng Identity UI

app.Run();
