using AutoMapper;
using BeGeneric.Context;
using BeGeneric.DTOModels;
using BeGeneric.Services.Authentication;
using BeGeneric.Services.Common;
using BeGeneric.Services.BeGeneric;
using BeGeneric.Services.BeGeneric.DatabaseStructure;
using BeGeneric.Utility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using System;
using System.Text;
using System.Text.Json;

namespace BeGeneric
{
    public class Startup
    {
        readonly string AllowSpecificOrigins = "_allowSpecificOrigins";
        readonly string AllowSpecificOriginsProd = "_allowSpecificOriginsProd";

        private static readonly string[] AllowedHeaders = new string[]
        {
            HeaderNames.Pragma,
            HeaderNames.AccessControlAllowOrigin,
            HeaderNames.Authorization,
            HeaderNames.ContentType,
            HeaderNames.CacheControl,
            HeaderNames.Expires
        };

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
               .AddJwtBearer(options =>
               {
                   options.TokenValidationParameters = new TokenValidationParameters
                   {
                       ValidateIssuer = true,
                       ValidateAudience = true,
                       ValidateLifetime = true,
                       ValidateIssuerSigningKey = true,
                       ValidIssuer = Configuration["Jwt:Issuer"],
                       ValidAudience = Configuration["Jwt:Issuer"],
                       IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Jwt:Key"]))
                   };
               });

            services.Configure<AppSettings>(Configuration.GetSection("AppSettings"));

            // Add development Cors configuration.
            services.AddCors(options =>
            {
                options.AddPolicy(name: AllowSpecificOrigins, builder =>
                {
                    builder.WithHeaders(AllowedHeaders);
                    builder.AllowAnyMethod();
                    // TODO: Add development origins!!
                    builder.WithOrigins("https://localhost:4200", "http://localhost:4200");
                });
                
                options.AddPolicy(name: AllowSpecificOriginsProd, builder =>
                {
                    builder.WithHeaders(AllowedHeaders);
                    builder.AllowAnyMethod();
                    // TODO: Add production origins!!
                    builder.WithOrigins("");
                });
            });

            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                });

            services.AddSwaggerGen(option =>
            {
                option.SwaggerDoc("v1", new OpenApiInfo { Title = "BeGeneric", Version = "v1" });
                option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Please enter a valid token",
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    BearerFormat = "JWT",
                    Scheme = "Bearer"
                });

                option.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            services.AddMemoryCache();

            // Auto mapping profile.
            services.AddAutoMapper(typeof(Startup));
            var mapperConfiguration = new MapperConfiguration(i => i.AddProfile(new AutoMapping()));
            services.AddSingleton<AutoMapper.IConfigurationProvider>(mapperConfiguration);

            // Generic Backend Common services
            AttachedActionService attachedActionService = new AttachedActionService();
            ConfigureAttachedActions(attachedActionService);
            ConfigureGenericServices(services, attachedActionService);

            // Customized Entity services
            services.AddDbContext<EntityDbContext>(item => item.UseSqlServer(Configuration.GetConnectionString("connectionString")));
            ConfigureOverwritingServices(services);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "BeGeneric v1"));
                app.UseCors(AllowSpecificOrigins);
            }
            else
            {
                app.UseCors(AllowSpecificOriginsProd);
            }

            app.UseHttpsRedirection();

            string storagePath = Configuration.GetSection("AppSettings").GetValue<string>("UploadStoragePath");

            if (!string.IsNullOrEmpty(storagePath))
            { 
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(storagePath),
                    RequestPath = Configuration.GetSection("AppSettings").GetValue<string>("UploadAccessUrl")
                });
            }

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        #region Generic Backend configuration

        private void ConfigureGenericServices(IServiceCollection services, AttachedActionService attachedActionService)
        {
            services.AddDbContext<ControllerDbContext>(item => item.UseSqlServer(Configuration.GetConnectionString("connectionString")));
            services.AddSingleton<IDatabaseStructureService>(new MsSqlDatabaseStructureService(Configuration.GetConnectionString("connectionString")));
            services.AddSingleton<IAttachedActionService>(attachedActionService);

            services.AddScoped<IGenericDataService, GenericDataService>();
            services.AddScoped<IPasswordService, PasswordService>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddSingleton<IMemoryCacheService, MemoryCacheService>();

            services.AddScoped<IAutocompleteService, AutocompleteService>();
            services.AddScoped<IImageService, ImageService>();
            services.AddScoped<IMessagingService, MessagingService>();
        }

        #endregion

        #region Custom implementation of Generic Backend services or additional services

        private static void ConfigureOverwritingServices(IServiceCollection services)
        {
        }

        private static void ConfigureAttachedActions(AttachedActionService attachedActionService)
        {
        }

        #endregion
    }
}
