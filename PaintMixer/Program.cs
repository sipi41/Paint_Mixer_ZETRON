using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MudBlazor.Services;
using PaintMixer.Application;
using PaintMixer.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents().AddCircuitOptions(options =>
    {
        options.DetailedErrors = true;
    })
    ;

// added emulator service...

builder.Services.AddSingleton<PaintMixerDeviceEmulator>();

// added API controllers

builder.Services.AddControllers();
    //.AddJsonOptions(options =>
    //{
    //    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    //}); // added to make JSON serialization consistent

// swagger for API documentation

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// limit API requests...

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter(policyName: "5PerSecond", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5; // Max requests
        limiterOptions.Window = TimeSpan.FromSeconds(1); // per 1 second
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0; // no queues
    });
});

// removal of automatic model state validation with 400 responses

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

// add Mud Blazor services

builder.Services.AddMudServices();

builder.Services.AddHttpClient();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); //swagger middleware
    app.UseSwaggerUI();  //swagger middleware
}

app.UseRateLimiter(); // API requests limiter

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();
