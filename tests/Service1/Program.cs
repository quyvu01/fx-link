using FxLink.Core.Abstractions;
using FxLink.Core.Extensions;
using Serilog;
using Service1.Dtos;
using Service1.PipelineBehaviors;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddFxLink(opts =>
{
    opts.AddConsumersFromAssemblies(typeof(Program).Assembly);
    opts.UseInMemory();
    opts.AddPublisherPipelineBehaviors(c => c
        .Of(typeof(PublishPipelineBehavior<>))
    );
    opts.AddConsumerPipelineBehaviors(c => c
        .Of<ConsumerPipelineBehavior>());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/placeOrder", async (IPublisher publisher) =>
    {
        await publisher.PublishAsync(new OrderPlaced { OrderId = Guid.NewGuid(), OrderTime = DateTime.UtcNow });
        return "Order placed";
    })
    .WithOpenApi();

app.MapPost("/cancelOrder", async (IPublisher publisher) =>
    {
        await publisher.PublishAsync(new OrderCancelled { OrderId = Guid.NewGuid(), CancelledTime = DateTime.UtcNow });
        return "Order cancelled";
    })
    .WithOpenApi();

app.Run();