using FinancialDetector.API.Middlewares;
using FinancialDetector.Domain.Interfaces;
using FinancialDetector.Infrastructure.Data;
using FinancialDetector.Infrastructure.Repositories;
using FinancialDetector.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Controller Kayýtlarý
builder.Services.AddControllers();

// 2. Swagger / OpenAPI Yapýlandýrmasý
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FinancialDetector.API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Lütfen oluþturulan Token deðerinizi buraya yapýþtýrýn."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// 3. Veritabaný Baðlantýsý
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 4. Mimari Kayýtlar (Dependency Injection)
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
// Not: Somut sýnýfýn adýnýn 'TransactionAnalyzerService' olduðunu varsayýyorum.
builder.Services.AddScoped<ITransactionAnalyzerService, TransactionAnalyzerService>();

// 5. JWT Kimlik Doðrulama Ayarlarý
var jwtKey = builder.Configuration["Jwt:Key"] ?? "GelistirmeOrtamiIcinGeciciGizliAnahtar123!!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ======================================================================
// OTOMASYON KATMANI: Yönlendirme Kurallarý
// ======================================================================

// Eðer tarayýcý ana dizine (/) gelirse otomatik olarak /swagger'a yolla
app.MapGet("/", () => Results.Redirect("/swagger"));

// Eðer tarayýcý senin inatçý index.html adresine gelirse yine /swagger'a yolla
app.MapGet("/index.html", () => Results.Redirect("/swagger"));

// ======================================================================
// MIDDLEWARE AKIÞI
// ======================================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global Hata Yönetimi
app.UseMiddleware<ExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();