using HeadLendingVisitReportApi.Data;
using HeadLendingVisitReportApi.Repositories;
using HeadLendingVisitReportApi.Services;

// Npgsql 6+ only accepts UTC DateTimes for 'timestamp with time zone' and rejects
// UTC DateTimes for 'timestamp'. The API previously sent whatever DateTime the
// client posted to SQL Server DATETIME2, so keep that (legacy) behaviour.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ---- MVC / Swagger ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var swaggerServerPrefix = builder.Configuration["Swagger:ServerPrefix"];
    if (!string.IsNullOrWhiteSpace(swaggerServerPrefix))
    {
        // So Swagger "Try it out" calls go through Kong: /headlendingvisitreport/api/...
        options.AddServer(new Microsoft.OpenApi.Models.OpenApiServer
        {
            Url = swaggerServerPrefix.TrimEnd('/'),
            Description = "Kong Gateway"
        });
    }

    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Head Lending Visit Report API",
        Version = "v1",
        Description = "ADO.NET (Npgsql) + PostgreSQL-function backend for the Visit Report of Head (Lending) form."
    });
});

// ---- CORS: the front-end is a static HTML file, so allow it to call this API from any origin in dev ----
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "*" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (allowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

// ---- Data access (ADO.NET, no ORM) ----
builder.Services.AddSingleton<IDbConnectionFactory, PostgresConnectionFactory>();
builder.Services.AddScoped<IVisitReportRepository, VisitReportRepository>();
builder.Services.AddScoped<IAttachmentRepository, AttachmentRepository>();

// ---- File storage on the local file system ----
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();

var app = builder.Build();

var enableSwagger = app.Environment.IsDevelopment()
    || app.Configuration.GetValue("Swagger:Enabled", false);

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        // Relative path works when Kong strips /headlendingvisitreport
        options.SwaggerEndpoint("v1/swagger.json", "Head Lending Visit Report API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors("Default");

// Serve uploaded visit-report files (App_Data/Uploads/...)
var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "App_Data", "Uploads");
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsRoot),
    RequestPath = "/App_Data/Uploads"
});

app.UseAuthorization();
app.MapControllers();

app.Run();
