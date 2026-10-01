using ClothingStore.Catalog.ApiClients;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Catalog never touches the DB: all data comes from ClothingStore.Api
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<TenantHeaderHandler>();

var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl is missing.");
builder.Services.AddHttpClient<StoreApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<TenantHeaderHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
