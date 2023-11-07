﻿using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using ADME.Models;
using ADME.Services;
using ADME.Services.Interfaces;
using Azure;
using Azure.Core.Serialization;
using Azure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;


WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddControllersWithViews();
builder.Services.AddRouting();
builder.Services.AddOptions();
builder.Services.Configure<OpenAiConfig>(builder.Configuration.GetSection("Azure:OpenAi"));

string searchServiceEndpoint = builder.Configuration["Azure:CognitiveSearch:Endpoint"];
string searchAlias = builder.Configuration["Azure:CognitiveSearch:SearchIndexAlias"];
string adminApiKey = builder.Configuration["Azure:CognitiveSearch:SearchAdminKey"];

builder.Services.AddAzureClients(az =>
{
    DefaultAzureCredentialOptions options = new()
    {
        ManagedIdentityClientId = builder.Configuration["AppSettings:managedIdentityClientId"]
    };

    az.ConfigureDefaults(builder.Configuration.GetSection("AzureDefaults"));
    az.UseCredential(new DefaultAzureCredential(options));
    JsonSerializerOptions serializerOptions = new()
    {
        Converters = {new MicrosoftSpatialGeoJsonConverter()},
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    az.AddSearchClient(new Uri(searchServiceEndpoint),
            searchAlias,
            new AzureKeyCredential(adminApiKey))
        .ConfigureOptions(op => op.Serializer = new JsonObjectSerializer(serializerOptions));

    az.AddOpenAIClient(
        new Uri(builder.Configuration["Azure:OpenAi:Endpoint"] ??
                throw new ArgumentNullException("Azure:OpenAi:Endpoint")),
        new AzureKeyCredential(builder.Configuration["Azure:OpenAi:ApiKey"] ??
                               throw new ArgumentNullException("Azure:OpenAi:ApiKey"))).WithName("OpenAi");
});

builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.AddScoped<ICognitiveSearchService, CognitiveSearchService>();

WebApplication app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();
app.MapGet("/", () => { return "Always On"; });

app.Run();