using Hangfire;
//using IMS.API.Middlewares.CHM;
//using IMS.API.Middlewares.GBM;
//using IMS.API.Middlewares.MM;
using IMS.API.ModelFilter;
using IMS.Application.Extensions;
using IMS.Application.Interfaces;
using IMS.Infrastructure.DBSeeder;
using IMS.Infrastructure.Extensions;
using IMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("GlobalLimiter", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromSeconds(30);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 2;
    });
});


builder.Services.AddSwaggerGen(c =>
{
    //c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        return true;
    });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter JWT Bearer token only"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

//builder.Services.Configure<JwtSetting>(builder.Configuration.GetSection("Jwt"));

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrEmpty(jwtKey))
    throw new InvalidOperationException("JWT Key is not configured");
if (string.IsNullOrEmpty(jwtIssuer))
    throw new InvalidOperationException("JWT Issuer is not configured");
if (string.IsNullOrEmpty(jwtAudience))
    throw new InvalidOperationException("JWT Audience is not configured");

var keyBytes = Encoding.UTF8.GetBytes(jwtKey!);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            RoleClaimType = ClaimTypes.Role
        };
    });


builder.Services.AddMemoryCache();
builder.Services.AddOpenApi();
builder.Services.AddHangfire(configuration =>
    configuration.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"), new Hangfire.SqlServer.SqlServerStorageOptions
    {
        SchemaName = "hangfire" ,
    }));

builder.Services.AddHangfireServer(options =>
{
    options.Queues = new[] { "critical", "email", "audit" };
});


builder.Services.AddApplicationServices();
builder.Services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<IMS_DbContext>());
//builder.Services.AddAuthorization();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("Everyone", policy =>
        policy.RequireRole("User","Admin"));

    //options.AddPolicy("ManagerOnly", policy =>
    //    policy.RequireRole("Admin", "Manager"));
});


var app = builder.Build();

// DB migrations and seeders
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var db = services.GetRequiredService<IMS_DbContext>();

    var RoleSeeder = services.GetRequiredService<Seeder>();
    await RoleSeeder.RoleSeeder();

    var AdminSeeder = services.GetRequiredService<Seeder>();
    var config = services.GetRequiredService<IConfiguration>();
    await AdminSeeder.AdminSeeder(config);
}

// recurring jobs
using (var scope = app.Services.CreateScope())
{
    var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJob>();
    recurringJobs.Register();
}

// Swagger middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
        c.RoutePrefix = string.Empty; // Swagger available at root here "/"
    });
}


app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

//// Custom middleware
//app.UseMetricsMiddleware();
//app.UseGlobalExceptionBuilder();
//app.UseCustomHeaderBuilder();
// Hangfire dashboard
app.UseHangfireDashboard("/hangfire");
// Map controllers 
app.MapControllers();
app.Run();
