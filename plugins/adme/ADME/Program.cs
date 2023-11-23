using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using ADME.Helpers;
using ADME.Models;
using ADME.Services;
using ADME.Services.Interfaces;
using Asp.Versioning;
using Azure;
using Azure.Core.Serialization;
using Azure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Web;


WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration)
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

builder.Services.AddControllers();

string[] corsUrls = builder.Configuration.GetSection("AllowedOrigins").Value?.Split(";") ??
                    throw new ArgumentException("CORS AllowedOrigins must be set in config.");
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithOrigins(corsUrls);
    });
});

builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("ReadAccess",
        policy => policy.RequireClaim("roles", "APIReader", "APIRestrictedReader")
            .RequireScope("access_as_user")); // role defined in API app manifest
    options.AddPolicy("WriteAccess",
        policy => policy.RequireClaim("roles", "APIWriter")
            .RequireScope("access_as_user")); // role defined in API app manifest
    options.AddPolicy("IndexSource.Write",
        policy => policy.RequireClaim("roles", "IndexSource.Write").RequireScope("access_as_user"));
    options.AddPolicy("IndexSource.Read",
        policy => policy.RequireClaim("roles", "IndexSource.Write", "IndexSource.Read").RequireScope("access_as_user"));
});

builder.Services.AddControllersWithViews();
builder.Services.AddRouting();

builder.Services.AddApiVersioning(c =>
    {
        c.DefaultApiVersion = new ApiVersion(0, 1);
        c.AssumeDefaultVersionWhenUnspecified = true;
        c.ReportApiVersions = true;
    })
    .AddMvc()
    .AddApiExplorer(
        options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

builder.AddSwagger(scopes: new[] {"access_as_user"});

string searchServiceEndpoint = builder.Configuration["Azure:CognitiveSearch:Endpoint"]!;
string searchAlias = builder.Configuration["Azure:CognitiveSearch:SearchIndexAlias"]!;
string? adminApiKey = Environment.GetEnvironmentVariable("COGNITIVESEARCH_ADMINKEY");

builder.Services.AddOptions();
builder.Services.Configure<OpenAiConfig>(builder.Configuration.GetSection("Azure:OpenAi"));
builder.Services.AddAzureClients(az =>
{
    az.ConfigureDefaults(builder.Configuration.GetSection("AzureDefaults"));
    az.UseCredential(new DefaultAzureCredential());
    JsonSerializerOptions serializerOptions = new()
    {
        Converters = {new MicrosoftSpatialGeoJsonConverter()},
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    az.AddSearchClient(new Uri(searchServiceEndpoint),
            searchAlias,
            new AzureKeyCredential(adminApiKey!))
        .ConfigureOptions(op => op.Serializer = new JsonObjectSerializer(serializerOptions));

    az.AddOpenAIClient(
        new Uri(builder.Configuration["Azure:OpenAi:Endpoint"] ??
                throw new ArgumentNullException("Azure:OpenAi:Endpoint")),
        new AzureKeyCredential(Environment.GetEnvironmentVariable("OPENAI_APIKEY") ??
                               throw new ArgumentNullException("Azure:OpenAi:ApiKey")));
});

builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.AddScoped<ICognitiveSearchService, CognitiveSearchService>();

WebApplication app = builder.Build();

app.UseSwaggerUi(builder.Configuration["Swagger:ClientId"]!, 
    builder.Configuration["AzureAd:ClientId"]!);
app.UseCors();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => { return "Always On"; });

app.Run();