using E_commerce.Areas.Admin.Controllers.RepositoryPattern;
using E_commerce.Areas.Admin.Repository;
using E_commerce.Controllers;
using E_commerce.Models;
using E_commerce.Repository;
using E_commerce.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Stripe;
using Stripe.Climate;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFlutter", policy =>
    {
        policy.AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

// Thêm UserFacade vào DI container
builder.Services.AddScoped<UserFacade>();

//Connection db
builder.Services.AddDbContext<DataContext>(options =>
{
    options.UseSqlServer(builder.Configuration["ConnectionStrings:ConnectedDb"]);
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();

/*// Đọc cấu hình Firebase từ appsettings.json
var firebaseConfig = builder.Configuration.GetSection("Firebase").Get<Dictionary<string, string>>();
// Truyền cấu hình vào View
builder.Services.AddSingleton(firebaseConfig);*/

//Email
var orderSubject = new OrderSubject();
var emailService = new EmailNotificationService(new EmailSender());

orderSubject.Attach(emailService);

builder.Services.AddSingleton(orderSubject);

builder.Services.AddTransient<IEmailSender, EmailSender>();
// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "E-Commerce API", Version = "v1" });
});

builder.Services.AddScoped<BrandApiController>();
builder.Services.AddScoped<CategoryApiController>();

// Đăng ký UserManager và RoleManager trước
builder.Services.AddScoped<UserManager<AppUserModel>>();
builder.Services.AddScoped<RoleManager<IdentityRole>>();

// Thêm UserFacade vào DI container
builder.Services.AddScoped<UserFacade>();

// Đăng ký RoleManagerDecorator sau khi đã có RoleManager
builder.Services.AddScoped<RoleManagerDecorator>();

builder.Services.AddSingleton<CouponFactory>();
builder.Services.AddScoped<CouponManager>();

builder.Services.AddScoped<IProductRepository, ProductRepository>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});


builder.Services.AddScoped<IProductService, ProductService>();


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

app.UseCors("AllowFlutter");

app.UseStatusCodePagesWithRedirects("/Home/Error?statuscode={0}");
app.UseSession();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    /*app.UseExceptionHandler("/Home/Error");*/
    app.UseStatusCodePagesWithRedirects("/Home/Index");
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "E-Commerce API v1");
        c.RoutePrefix = "swagger";
    });

    app.UseStatusCodePagesWithRedirects("/Home/Index");
}

app.UseStaticFiles();
app.MapHub<ChatHub>("/chatHub");
app.UseRouting();



StripeConfiguration.ApiKey = builder.Configuration.GetSection("Stripe:SecretKey").Get<string>();

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
