
using ClusterManager.Interfaces;
using ClusterManager.Services;

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
            builder.Services.AddScoped<INodeRegistry, NodeRegistry>();
            builder.Services.AddScoped<INodeManager, NodeManager>();
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddHttpClient();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseDefaultFiles();
            app.UseStaticFiles();
            
            app.MapControllers();

            app.Run();
        }
    }
}
