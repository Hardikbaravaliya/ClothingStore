using ClothingStore.Infrastructure;
using ClothingStore.Infrastructure.Tenancy;
using ClothingStore.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAppServices();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "ClothingStore API", Version = "v1" });

    // Lets you pick the store in Swagger UI ("Authorize" button => X-Tenant)
    o.AddSecurityDefinition("Tenant", new OpenApiSecurityScheme
    {
        Name = ApiTenantMiddleware.HeaderName,
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Store slug, e.g. shop1",
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Tenant" } }] = [],
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();
// Phase 3: app.UseAuthentication() (customer JWT) goes here, before the tenant middleware
app.UseMiddleware<ApiTenantMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
