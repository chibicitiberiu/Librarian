using Librarian.DB;
using Librarian.Indexing;
using Librarian.Metadata.Providers;
using Librarian.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.IO;
using System.Net;

namespace Librarian
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddDbContext<DatabaseContext>(opts =>
                opts.UseNpgsql(builder.Configuration.GetConnectionString("DB"))
            );

            builder.Services.AddControllersWithViews();
            builder.Services.AddQuartz(opts =>
            {
                opts.AddJob<IndexingJob>(IndexingJob.Key, job => job.StoreDurably().DisallowConcurrentExecution());
                opts.AddJob<MetadataUpdateJob>(MetadataUpdateJob.Key, job => job.StoreDurably());

                //opts.AddTrigger(trigger =>
                //        trigger.ForJob(IndexingJob.Key)
                //               .WithSimpleSchedule(SimpleScheduleBuilder.RepeatHourlyForever(1))
                //               .UsingJobData("mode", "quick"));

                //opts.AddTrigger(trigger =>
                //        trigger.ForJob(IndexingJob.Key)
                //               .WithSimpleSchedule(SimpleScheduleBuilder.RepeatHourlyForever(24 * 7))
                //               .UsingJobData("mode", "full"));
            });
            builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

            builder.Services.AddSingleton<FileService>();
            builder.Services.AddScoped<MetadataService>();

            builder.Services.AddScoped<IMetadataProvider, FileMetadataProvider>();
            builder.Services.AddScoped<IMetadataProvider, MetadataExtractorProvider>();

            builder.Services.AddSession(opts =>
            {
                opts.Cookie.HttpOnly = true;
                opts.Cookie.IsEssential = true;
                opts.IdleTimeout = TimeSpan.FromMinutes(10);
            });

            var app = builder.Build();

            try
            {
                VerifyConfiguration(app.Configuration);
            }
            catch (Exception ex)
            {
                app.Logger.LogCritical("Configuration error: {}\nFix the configuration file errors and try again!", ex.Message);
                Environment.Exit(-1);
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            //app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthorization();
            app.UseSession();

            app.MapControllerRoute("default",
                                    "",
                                    new { controller = "Home", action = "Index" });

            app.MapControllerRoute("error",
                                    "/error/{id?}",
                                    new { controller = "Home", action = "Error" });

            app.MapControllerRoute("Browse",
                                    "browse/{**path}",
                                    new { controller = "Browse", action = "Index", path = string.Empty });

            app.MapControllerRoute("Browse_Cut", "browse_actions/cut", new { controller = "Browse", action = "Cut" });
            app.MapControllerRoute("Browse_Copy", "browse_actions/copy", new { controller = "Browse", action = "Copy" });
            app.MapControllerRoute("Browse_Paste", "browse_actions/paste", new { controller = "Browse", action = "Paste" });
            app.MapControllerRoute("Browse_Rename", "browse_actions/rename", new { controller = "Browse", action = "Rename" });
            app.MapControllerRoute("Browse_Delete", "browse_actions/delete", new { controller = "Browse", action = "Delete" });

            app.MapControllerRoute("Search",
                                    "/search",
                                    new { controller = "Search", action = "Index" });

            app.MapControllerRoute("AdvancedSearch",
                                    "/advanced_search",
                                    new { controller = "Search", action = "Advanced" });

            app.MapControllerRoute("Metadata",
                                    "metadata/{**path}",
                                    new { controller = "Metadata", action = "Index", path = string.Empty });

            app.Run();
        }


        private static void VerifyConfiguration(IConfiguration config)
        {
            // ensure BaseDirectory is set 
            var baseDirectory = config["BaseDirectory"]
                ?? throw new ArgumentException("Required BaseDirectory option is not set!");

            if (!Directory.Exists(baseDirectory))
                throw new ArgumentException("BaseDirectory does not exist!");
        }
    }
}