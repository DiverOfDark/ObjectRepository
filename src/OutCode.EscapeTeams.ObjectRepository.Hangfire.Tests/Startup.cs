using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OutCode.EscapeTeams.ObjectRepository.EventStore;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire.Tests
{
    public class DummyObjectRepository : ObjectRepositoryBase
    {
        public DummyObjectRepository(ILogger logger): base(CreateStorage(), logger)
        {
            this.RegisterHangfireScheme();
            Initialize();
        }

        private static IStorage CreateStorage() => new DummyStorage();

        private class DummyStorage : IStorage
        {
            private ConcurrentList<object> items = new ConcurrentList<object>();
            
            public Task SaveChanges() => Task.CompletedTask;
            public async Task<IEnumerable<T>> GetAll<T>() where T:BaseEntity => items.OfType<T>().ToList();

            public void Track(ITrackable trackable, bool isReadonly)
            {
                trackable.ModelChanged += handler;
            }

            private void handler(ModelChangedEventArgs obj)
            {
                switch (obj.ChangeType)
                {
                    case ChangeType.Add:
                        items.Add(obj.Source);
                        break;
                    case ChangeType.Remove:
                        items.Remove(obj.Source);
                        break;
                }
            }

            public event Action<Exception> OnError;
        }
    }
    
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var objectRepository = new DummyObjectRepository(NullLogger.Instance);
            services.AddLogging();
            services.AddHangfire(s => s.UseHangfireStorage(objectRepository).UseColouredConsoleLogProvider());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHangfireServer();
            app.UseHangfireDashboard();
            RecurringJob.AddOrUpdate("testjob", () => TestMethod(1, "my param"), Cron.Daily);
            RecurringJob.AddOrUpdate("longjob", () => LongTestMethod(1, "my param"), Cron.Daily);
            RecurringJob.AddOrUpdate("failjob", () => FailMethod(2, "fail"), Cron.Daily);

            for (char a = 'a'; a <= 'z'; a++)
            {
                var a1 = a;
                var name = "TestSorting_" + a;
                RecurringJob.AddOrUpdate(name, () => TestMethod(a1, name), Cron.Yearly);
            }
            
            app.Run(async (context) => { await context.Response.WriteAsync("Hello World!"); });
        }

        public void FailMethod(int i, string fail)
        {
            throw new Exception(fail);
        }

        public void TestMethod(int i, string myParam)
        {
            Thread.Sleep(5000);
        }

        public void LongTestMethod(int i, string myParam)
        {
            Thread.Sleep(60000);
        }

    }
}