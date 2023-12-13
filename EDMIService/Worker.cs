using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReportApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EDMIService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected  override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //int interval = Program._settings.ReadInterval * 60 * 1000;
            int interval = 20000;

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                Dowork();
                await Task.Delay(interval, stoppingToken);
            }
        }

        static void Dowork()
        {
            //Program.Init();
            try
            {
                Program.DoWork();
            }
            catch(Exception ex) { Util.Logging("Dowork Error",ex.Message); }
        }
    }
}
