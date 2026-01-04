using IMS.Application.Interfaces;
using IMS.Application.Settings;
using IMS.Domain.Entities;
using IMS.Infrastructure.CloudWatch;
using IMS.Infrastructure.DBSeeder;
using IMS.Infrastructure.Mailer;
using IMS.Infrastructure.Persistence;
using IMS.Infrastructure.Token;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IMS.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string not found. Check user secrets.");
            }

            services.AddDbContext<IMS_DbContext>(options =>
                options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.CommandTimeout(60
                )
              ));

            services.AddIdentity<AppUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 6;
            })
            .AddEntityFrameworkStores<IMS_DbContext>()
            .AddDefaultTokenProviders();

            services.Configure<AwsSettings>(configuration.GetSection("AWS"));
            services.AddSingleton<ICloudWatchLogger, CloudWatchLogger>();
            services.Configure<CloudinarySettings>(configuration.GetSection("CloudinarySettings"));
            services.Configure<JwtSetting>(configuration.GetSection("Jwt"));
            services.Configure<SMTPSettings>(configuration.GetSection("EmailSettings"));
            services.AddScoped<Seeder>();
            services.AddScoped<ITokenGenerator, TokenGenerator>();
            services.AddTransient<IMailerService, MailService>();

            return services;
        }
    }
}
