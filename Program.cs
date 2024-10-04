using UnsubscribeService.Cache;
using UnsubscribeService.Interfaces;
using UnsubscribeService.ServiceAttributes;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using UnsubscribeService.Middlewares;
using UnsubscribeService.EmailTemplate;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddLogging(config =>
{
    config.ClearProviders();
    config.AddConsole();
});

builder.Services.AddScoped<ICustomMemoryCache, CustomMemoryCache>();
builder.Services.AddScoped<ManageAuthenticationAttribute>();
builder.Services.AddScoped<ITemplateService, TemplateService>();

#region << Authentication globally >>

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ManageAuthenticationAttribute>();
});

#endregion

#region << Register the cache and store the templates >>

var templates = new Dictionary<Guid, string>
{
    { new Guid("11111111-1111-1111-1111-111111111111"), "<html>Template 1 with token: {0} and email: {1}</html>" },
    { new Guid("22222222-2222-2222-2222-222222222222"), "<html>Template 2 with token: {0} and email: {1}</html>" }
};

builder.Services.AddScoped<ICustomMemoryCache, CustomMemoryCache>(serviceProvider =>
{
    var memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
    var logger = serviceProvider.GetRequiredService<ILogger<CustomMemoryCache>>();

    var cache = new CustomMemoryCache(memoryCache, logger); 
    foreach (var template in templates)
    {
        cache.Set(template.Key.ToString(), template.Value, TimeSpan.FromHours(1)); // Cache for 1 hour
    }
    return cache;
});

#endregion

#region << Swagger Authorization >>

builder.Services.AddSwaggerGen(options =>
{
    // Basic Authentication for Swagger
    options.AddSecurityDefinition("basic", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        In = ParameterLocation.Header,
        Description = "\"Input your Basic Authentication credentials (Username:Password) to access this API\""
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "basic"
                }
            },
            new string[]{}
        }
    });

    options.OperationFilter<SecurityRequirementsOperationFilter>();
});

#endregion

#region << Rate Limiting >>

builder.Services.AddRateLimiter(options =>
{
    // Limit to 100 requests per 10 minutes per client IP
    options.AddFixedWindowLimiter("BasicRateLimiter", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(10);
        limiterOptions.QueueLimit = 2; ;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});




#endregion


var app = builder.Build();

var configuration = builder.Configuration;

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();


if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

if (builder.Configuration.GetValue<bool>("EnableSwagger", false))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (builder.Configuration.GetValue<bool>("UseHttpsRedirection", false))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();