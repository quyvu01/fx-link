using FxLink.Extensions;
using FxLink.RabbitMq.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;

// Plain generic host, no ASP.NET Core: Payment only reacts to messages, it has no HTTP surface.
// This also proves FxLink doesn't require a web pipeline to run.
var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddFxLink(opts =>
{
    opts.AddConsumersFromAssemblies(typeof(Program).Assembly);

    opts.AddConsumerDefinitionsFromAssemblies(typeof(Program).Assembly);

    opts.AddRabbitMq(config => config.Host("localhost", "fxlink"));
});

var app = builder.Build();

await app.RunAsync();
