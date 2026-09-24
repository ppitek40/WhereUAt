using Api.Services;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .Configure<MongoDbSettings>(builder.Configuration
        .GetSection(MongoDbSettings.SectionName));

builder.Services
    .AddInfrastructure()
    .AddApplication();

builder.Services.AddGrpc(options => options.EnableDetailedErrors = true);

var app = builder.Build();

app.MapGrpcService<FencesGrpcService>();

app.Run();