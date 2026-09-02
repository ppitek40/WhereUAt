using Api.Endpoints;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .Configure<MongoDbSettings>(builder.Configuration
        .GetSection(MongoDbSettings.SectionName));

builder.Services
    .AddInfrastructure()
    .AddApplication();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapCreateFence();

app.Run();