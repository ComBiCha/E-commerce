using E_commerce.Areas.Admin.Repository;
using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

//Connection db
builder.Services.AddDbContext<DataContext>(options =>
{
    options.UseSqlServer(builder.Configuration["ConnectionStrings:ConnectedDb"]);
});

//Email
builder.Services.AddTransient<IEmailSender, EmailSender>();
// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.IsEssential = true;
});

builder.Services.AddIdentity<AppUserModel,IdentityRole>()
    .AddEntityFrameworkStores<DataContext>().AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    // Password settings.
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 4;
    options.Password.RequiredUniqueChars = 1;

/*    // Lockout settings.
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;*/

    // User settings.
    options.User.AllowedUserNameCharacters =
    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true;
});

var app = builder.Build();

app.UseStatusCodePagesWithRedirects("/Home/Error?statuscode={0}");
app.UseSession();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "Areas",
    pattern: "{area:exists}/{controller=Product}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "category",
    pattern: "/category/{Slug?}",
    defaults : new {controller="Category",action="Index"});

app.MapControllerRoute(
    name: "brand",
    pattern: "/brand/{Slug?}",
    defaults: new { controller = "Brand", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");



//Seeding data
var context = app.Services.CreateScope().ServiceProvider.GetRequiredService<DataContext>();
SeedData.SeedingData(context);

app.Run();
