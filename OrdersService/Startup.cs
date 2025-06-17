using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using Microsoft.OpenApi.Models;
using OrdersService.BackgroundServices;
using OrdersService.Messaging;

namespace OrdersService;

public class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration) => Configuration = configuration;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();

        services.AddDbContext<OrdersDbContext>(options =>
            options.UseNpgsql(Configuration.GetConnectionString("DefaultConnection")));

        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                builder => { builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader(); });
        });

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Orders API", Version = "v1" }); 
            c.EnableAnnotations();
        });
        services.AddHostedService<OutboxProcessorService>();
        services.AddHealthChecks();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, OrdersDbContext dbContext, IServiceProvider serviceProvider)
    {
        dbContext.Database.Migrate();
        
        var consumer = new MessageConsumer(serviceProvider.GetRequiredService<IServiceScopeFactory>());
        Task.Run(() => consumer.StartListening());
        
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Orders API"));
        }

        app.UseRouting();
        app.UseCors("AllowAll");
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");
            });
    }
}