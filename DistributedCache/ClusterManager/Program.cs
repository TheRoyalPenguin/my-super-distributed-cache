using Newtonsoft.Json.Converters;
using ClusterManager.Interfaces;
using ClusterManager.Services;
using ClusterManager.Services.BackgroundServices;

namespace ClusterManager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddHttpClient<IHttpService, HttpService>();
            builder.Services.AddSingleton<ICacheStorage, CacheStorage>();
            builder.Services.AddHostedService<NodeRestoreService>();
            builder.Services.AddHostedService<NodeStatusChecker>();
            builder.Services.AddScoped<INodeManager, NodeManager>();
            builder.Services.AddScoped<INodeRegistry, NodeRegistry>();
            builder.Services
                .AddControllers()
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.Converters.Add(new StringEnumConverter());
                });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
