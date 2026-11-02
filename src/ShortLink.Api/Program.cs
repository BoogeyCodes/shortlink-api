using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ShortLink.Api.Common;
using ShortLink.Api.Endpoints;
using ShortLink.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=shortlink.db"));

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddSingleton<ICodeGenerator, CodeGenerator>();
builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddExceptionHandler<ExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "ShortLink API", Version = "v1" });
    // Feature slices nest their DTOs (CreateLink.Response, GetLinkStats.Response...),
    // so include the outer type name to keep schema ids unique.
    options.CustomSchemaIds(t => t.DeclaringType is null ? t.Name : t.DeclaringType.Name + t.Name);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Health").ExcludeFromDescription();
app.MapLinkEndpoints();

app.Run();

// Exposes the implicit Program class to the integration test project.
public partial class Program;
