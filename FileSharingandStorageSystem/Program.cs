using Amazon.S3;
using Amazon.Runtime;
using FileSharingandStorageSystem;
using FileSharingandStorageSystem.Interfaces;
using FileSharingandStorageSystem.Models;
using FileSharingandStorageSystem.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

// Behind a TLS-terminating proxy (Render, Fly.io, Nginx), honor X-Forwarded-*
// so HTTPS redirection and cookie security see the original client scheme
// instead of the internal HTTP hop, avoiding redirect loops.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IFileShareService, FileShareService>();

// File byte storage: use an S3-compatible bucket (Cloudflare R2 / AWS S3) when a
// bucket is configured, otherwise fall back to local disk for development.
var s3Bucket = builder.Configuration["Storage:S3:Bucket"];
if (!string.IsNullOrWhiteSpace(s3Bucket))
{
    builder.Services.AddSingleton<IAmazonS3>(_ =>
    {
        var cfg = builder.Configuration.GetSection("Storage:S3");
        var s3Config = new AmazonS3Config
        {
            ServiceURL = cfg["ServiceUrl"],
            // R2 and MinIO require path-style addressing rather than virtual-hosted buckets.
            ForcePathStyle = true,
            AuthenticationRegion = string.IsNullOrWhiteSpace(cfg["Region"]) ? "auto" : cfg["Region"]
        };
        var credentials = new BasicAWSCredentials(cfg["AccessKey"], cfg["SecretKey"]);
        return new AmazonS3Client(credentials, s3Config);
    });
    builder.Services.AddScoped<IObjectStorage, S3ObjectStorage>();
}
else
{
    builder.Services.AddScoped<IObjectStorage, LocalObjectStorage>();
}

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddDbContext<AppDBContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
})
    .AddEntityFrameworkStores<AppDBContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

var app = builder.Build();

// Apply pending migrations (creates the database/schema on first run).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
    db.Database.Migrate();
}

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Files/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Files}/{action=Index}/{id?}");
});

app.Run();
