using System.Text;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.Web;


var logger =
NLog.LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();
logger.Debug("init main");
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Logging.ClearProviders();
builder.Host.UseNLog();

string mySecret = Environment.GetEnvironmentVariable("Secret") ?? "none";
string myIssuer = Environment.GetEnvironmentVariable("Issuer") ?? "none";
builder.Services
    .AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = myIssuer,
            ValidAudience = "http://localhost",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(mySecret))
        };

    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();