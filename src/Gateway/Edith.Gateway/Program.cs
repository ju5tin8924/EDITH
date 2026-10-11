using System.Text;
using Edith.Gateway.Endpoints;
using Edith.Gateway.Middleware;
using Edith.Gateway.Models;
using Edith.Gateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. JWT Bearer Authentication
var secretKey = builder.Configuration["Jwt:Secret"] ?? JwtTokenService.DefaultSecret;
var keyBytes = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // Dev / Local container friendly
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? JwtTokenService.Issuer,
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? JwtTokenService.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// 2. Authorization Policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AuthenticatedTenant", policy =>
    {
        policy.RequireAuthenticatedUser();
    });

// 3. IAM & Tenant Resolution Services
builder.Services.AddSingleton<ITenantRegistry, InMemoryTenantRegistry>();
builder.Services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IEntitlementsService, EntitlementsService>();

// 4. YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseRouting();

// 5. Auth and Tenant Pipeline
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();

// 6. Map Endpoints
app.MapIamEndpoints();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "Edith.Gateway",
    version = "1.0.0",
    timestamp = DateTimeOffset.UtcNow
})).AllowAnonymous();

app.MapReverseProxy();

app.Run();

public partial class Program
{
    protected Program() { }
}
