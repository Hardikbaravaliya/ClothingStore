using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ClothingStore.Api;
using ClothingStore.Api.Auth;
using ClothingStore.Contracts;
using ClothingStore.Infrastructure;
using ClothingStore.Infrastructure.Identity;
using ClothingStore.Infrastructure.Storage;
using ClothingStore.Infrastructure.Tenancy;
using ClothingStore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAppServices();

// ---- Customer identity (no cookies: the Api issues JWTs) ----
builder.Services
    .AddIdentityCore<ApplicationUser>(o =>
    {
        IdentitySetup.ConfigureOptions(o);
        o.SignIn.RequireConfirmedEmail = true; // customers must verify their email before logging in
    })
    .AddRoles<ApplicationRole>()
    .AddSignInManager()
    .AddAppStores();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<JwtTokenService>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // keep our claim names (sub, tenant_id, customer_id)
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.GetSecurityKey(),
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "sub",
            RoleClaimType = ClaimTypes.Role,
        };
    });
builder.Services.AddAuthorization();

// ---- Brute-force protection for login / register / password reset ----
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(RateLimits.Auth, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "ClothingStore API", Version = "v1" });

    // "Authorize" in Swagger UI: store (X-Tenant) + customer token
    o.AddSecurityDefinition("Tenant", new OpenApiSecurityScheme
    {
        Name = ApiHeaders.Tenant,
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Store slug, e.g. shop1",
    });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "accessToken from POST /api/account/login",
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Tenant" } }] = [],
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
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
app.UseLocalImageFiles(); // /uploads (product images for the Catalog)
app.UseAuthentication();  // before the tenant check: a token from another store is rejected there
app.UseMiddleware<ApiTenantMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();
