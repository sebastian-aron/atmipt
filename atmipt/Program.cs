using atmipt.Controllers;
using atmipt.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSession();
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
HomeController.BankData.BankCards = new List<BankCard>
    {
        new BankCard { Name = "BDO", Color = "#0033A0", Logo = "bdo.png" },
        new BankCard { Name = "China Bank", Color = "#D71A28", Logo = "cbs.png" },
        new BankCard { Name = "PNB", Color = "#004990", Logo = "pnb.png" },
        new BankCard { Name = "LandBank", Color = "#008000", Logo = "landbank.png" },
        new BankCard { Name = "BPI", Color = "#B1116", Logo = "bpi.png" },
        new BankCard { Name = "Visa", Color = "#1a1f71", Logo = "visa.png" },
        new BankCard { Name = "Mastercard", Color = "#EB001B", Logo = "mastercard.png" },
        new BankCard { Name = "American Express", Color = "#00267F", Logo = "amex.png" }
    };

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.UseStaticFiles();
app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();