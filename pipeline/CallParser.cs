using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Pipeline
{
    public static class CallParser
    {

        public static IHost ExtractData(this IHost host, IConfiguration Configuration)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<Parser>>();

            try
            {
                logger.LogInformation($"Extracting Data");
                new Parser(Configuration.GetSection("Scrapper"), logger).Extract("//ul[@class=\"nav nav-list\"]/li[1]/ul/li").Wait();
                logger.LogInformation($"Extraction completed");
            }
            catch (Exception ex)
            {
                logger.LogError($"An error occurred running extraction - {ex.Message}");
            }
            return host;
        }

    }
}