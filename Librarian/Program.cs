using Librarian.DB;
using Librarian.Indexing;
using Librarian.Jobs;
using Librarian.Metadata;
using Librarian.Metadata.Archives;
using Librarian.Metadata.Providers;
using Librarian.Metadata.Providers.MetadataCli;
using Librarian.Metadata.Normalization;
using Librarian.Metadata.Providers.ExifTool;
using Librarian.Metadata.Providers.Tika;
using Librarian.Library;
using Librarian.Services;
using Librarian.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Librarian
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // User-managed settings (edited from the Settings page) live in a writable settings.json under
            // the AppDataDirectory. Added last so it takes precedence over appsettings.json and environment
            // variables — once a value is saved in-app it is authoritative. Missing file is fine (optional).
            var appDataDir = PathUtils.GetCanonicalPath(builder.Configuration["AppDataDirectory"] ?? "data");
            builder.Configuration.AddJsonFile(Path.Combine(appDataDir, "settings.json"),
                                              optional: true, reloadOnChange: false);

            // Classification lists (file-type/MIME classes, sidecar roles, heuristic tokens & patterns) are
            // data, not code: load every config.d/*.json, shipped defaults first then AppDataDirectory
            // overrides, merged into the "Classification" section.
            // Merge every config.d/*.json (shipped first, then AppDataDirectory overrides) ourselves, with
            // ARRAY-REPLACE semantics: objects merge recursively, but a list (or scalar) in a later file
            // replaces the earlier one wholesale. IConfiguration's default would merge arrays element-wise
            // by index, so a shorter override would only overwrite the first N entries — a sharp edge for
            // per-deployment list customization. We feed the cleanly-merged result back into config.
            var cfgFiles = new List<string>();
            foreach (var cfgDir in new[]
                     {
                         Path.Combine(builder.Environment.ContentRootPath, "config.d"),
                         Path.Combine(appDataDir, "config.d"),
                     })
            {
                if (Directory.Exists(cfgDir))
                    cfgFiles.AddRange(Directory.GetFiles(cfgDir, "*.json").OrderBy(f => f, StringComparer.Ordinal));
            }
            int cfgFilesLoaded = cfgFiles.Count;
            if (cfgFiles.Count > 0)
                builder.Configuration.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(MergeConfigFiles(cfgFiles))));

            // Bind once and install into the (static, read-only-after-startup) classifiers, so the parallel
            // indexing threads share immutable lists. Falls back to the built-in defaults if config is absent.
            var boundClassification = builder.Configuration.GetSection("Classification").Get<ClassificationOptions>();
            var classification = boundClassification ?? ClassificationOptions.Default;
            Sidecars.Configure(classification);
            FolderHeuristics.Configure(classification);
            builder.Services.AddSingleton(classification);

            // Add services to the container.
            builder.Services.AddDbContext<DatabaseContext>(opts =>
                opts.UseNpgsql(builder.Configuration.GetConnectionString("DB"))
            );

            builder.Services.AddControllersWithViews();
            builder.Services.AddQuartz(opts =>
            {
                opts.AddJob<IndexingJob>(IndexingJob.Key, job => job.StoreDurably().DisallowConcurrentExecution());
                opts.AddJob<MetadataUpdateJob>(MetadataUpdateJob.Key, job => job.StoreDurably());

                opts.AddTrigger(trigger =>
                        trigger.ForJob(IndexingJob.Key)
                               .WithSimpleSchedule(SimpleScheduleBuilder.RepeatHourlyForever(24)));
                               //.UsingJobData("mode", "quick"));

                //opts.AddTrigger(trigger =>
                //        trigger.ForJob(IndexingJob.Key)
                //               .WithSimpleSchedule(SimpleScheduleBuilder.RepeatHourlyForever(24 * 7))
                //               .UsingJobData("mode", "full"));
            });
            builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
            builder.Services.AddSingleton<JobTracker>();

            builder.Services.AddSingleton<FileService>();
            builder.Services.AddSingleton<IndexingService>();
            builder.Services.AddHostedService(provider => provider.GetService<IndexingService>()!);
            builder.Services.AddSingleton<ProviderExecutor>();
            builder.Services.AddScoped<MetadataService>();
            builder.Services.AddScoped<RenormalizationService>();
            builder.Services.AddScoped<ItemAssociationService>();
            builder.Services.AddScoped<SearchVectorService>();
            builder.Services.AddScoped<ChecksumService>();
            builder.Services.AddScoped<SearchService>();
            builder.Services.AddScoped<LibraryService>();
            builder.Services.AddScoped<CollectionService>();
            builder.Services.AddScoped<MetadataFactory>();
            builder.Services.AddScoped<MetadataSerializer>();

            builder.Services.AddScoped<IMetadataProvider, FileMetadataProvider>();
            builder.Services.AddScoped<IMetadataProvider, FilenameMetadataProvider>();
            //builder.Services.AddScoped<IMetadataProvider, MetadataExtractorProvider>();
            builder.Services.AddScoped<IMetadataProvider, MetadataCliProvider>();
            builder.Services.AddScoped<IRawMetadataProvider, TikaProvider>();
            // ExifTool augments Tika with deeper embedded image/media tags; both raw providers run.
            builder.Services.AddScoped<IRawMetadataProvider, ExifToolProvider>();
            builder.Services.AddSingleton<MetadataNormalizer>();
            builder.Services.AddSession(opts =>
            {
                opts.Cookie.HttpOnly = true;
                opts.Cookie.IsEssential = true;
                opts.IdleTimeout = TimeSpan.FromMinutes(10);
            });

            builder.Services.AddSingleton<MetadataCliService>();
            builder.Services.AddSingleton<TikaService>();
            builder.Services.AddSingleton<ExifToolService>();

            // Archive entry byte access (collection_plan.md §7.3): read/materialize entries out of archives
            // for dedup hashing and path-based provider re-extraction. ZIP is built-in; other families
            // register their own source.
            builder.Services.AddSingleton<IArchiveByteSource, ZipArchiveByteSource>();
            builder.Services.AddSingleton<ArchiveByteReader>();

            var app = builder.Build();

            app.Logger.LogInformation(
                "Classification: {n} config.d file(s) loaded; using {src} ({v} video / {a} audio / {i} image ext, {sub} subtitle ext, {noise} noise tokens).",
                cfgFilesLoaded, boundClassification != null ? "config.d" : "built-in defaults",
                classification.VideoExtensions.Length, classification.AudioExtensions.Length,
                classification.ImageExtensions.Length, classification.SubtitleExtensions.Length,
                classification.NoiseTokens.Length);

            try
            {
                VerifyConfiguration(app.Configuration);
            }
            catch (Exception ex)
            {
                app.Logger.LogCritical("Configuration error: {}\nFix the configuration file errors and try again!", ex.Message);
                Environment.Exit(-1);
            }

            ApplyMigrations(app);

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                // Friendly 500 page in production; the developer exception page stays on in Development.
                app.UseExceptionHandler("/error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            // Friendly error page for status codes with no body (404, 403, …) in every environment.
            // Re-executes the request against /error/{code} so the wm-styled page is rendered in place.
            app.UseStatusCodePagesWithReExecute("/error/{0}");

            //app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthorization();
            app.UseSession();

            // The file browser is the application's landing surface (the new UI has no separate Home).
            app.MapControllerRoute("default",
                                    "",
                                    new { controller = "Browse", action = "Index", path = string.Empty });

            app.MapControllerRoute("error",
                                    "/error/{id?}",
                                    new { controller = "Home", action = "Error" });

            app.MapControllerRoute("Browse",
                                    "browse/{**path}",
                                    new { controller = "Browse", action = "Index", path = string.Empty });

            // Distinct prefix so the in-window file viewer isn't swallowed by the "browse/{**path}" catch-all.
            app.MapControllerRoute("Browse_Viewer", "browse_view/{**path}",
                                    new { controller = "Browse", action = "Viewer", path = string.Empty });

            app.MapControllerRoute("Browse_Cut", "browse_actions/cut", new { controller = "Browse", action = "Cut" });
            app.MapControllerRoute("Browse_Copy", "browse_actions/copy", new { controller = "Browse", action = "Copy" });
            app.MapControllerRoute("Browse_Paste", "browse_actions/paste", new { controller = "Browse", action = "Paste" });
            app.MapControllerRoute("Browse_Rename", "browse_actions/rename", new { controller = "Browse", action = "Rename" });
            app.MapControllerRoute("Browse_Delete", "browse_actions/delete", new { controller = "Browse", action = "Delete" });
            // Distinct prefix so it isn't swallowed by the "browse/{**path}" catch-all above.
            app.MapControllerRoute("Browse_Download", "browse_download/{**path}",
                                    new { controller = "Browse", action = "Download", path = string.Empty });

            app.MapControllerRoute("Library",
                                    "library/{category}/{view?}",
                                    new { controller = "Library", action = "Index" });

            app.MapControllerRoute("Collections",
                                    "collections",
                                    new { controller = "Collection", action = "Roots" });
            app.MapControllerRoute("Collection_Save", "collection_actions/save",
                                    new { controller = "Collection", action = "Save" });
            app.MapControllerRoute("Collection",
                                    "collection/{id:int}",
                                    new { controller = "Collection", action = "Index" });

            app.MapControllerRoute("Duplicates",
                                    "duplicates",
                                    new { controller = "Duplicates", action = "Index" });

            app.MapControllerRoute("Admin",
                                    "admin/{action=Index}",
                                    new { controller = "Admin" });

            app.MapControllerRoute("Search",
                                    "/search",
                                    new { controller = "Search", action = "Index" });

            app.MapControllerRoute("AdvancedSearch",
                                    "/advanced_search",
                                    new { controller = "Search", action = "Advanced" });

            app.MapControllerRoute("Metadata_Save", "metadata_actions/save", new { controller = "Metadata", action = "Save" });

            app.MapControllerRoute("Metadata",
                                    "metadata/{**path}",
                                    new { controller = "Metadata", action = "Index", path = string.Empty });

            app.Run();
        }

        /// <summary>Deep-merges config.d JSON files in order: nested objects merge recursively, but arrays
        /// and scalars from a later file REPLACE earlier ones wholesale (so an override list is a clean
        /// replacement, not IConfiguration's index-wise array merge). A malformed file is skipped, not fatal.</summary>
        private static string MergeConfigFiles(IEnumerable<string> files)
        {
            var docOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            var merged = new JsonObject();
            foreach (var file in files)
            {
                JsonNode? node;
                try { node = JsonNode.Parse(File.ReadAllText(file), documentOptions: docOptions); }
                catch { continue; }
                if (node is JsonObject obj)
                    MergeJsonInto(merged, obj);
            }
            return merged.ToJsonString();
        }

        private static void MergeJsonInto(JsonObject target, JsonObject source)
        {
            foreach (var kv in source)
            {
                if (kv.Value is JsonObject srcObj && target[kv.Key] is JsonObject tgtObj)
                    MergeJsonInto(tgtObj, srcObj);
                else
                    target[kv.Key] = kv.Value?.DeepClone();
            }
        }

        private static void VerifyConfiguration(IConfiguration config)
        {
            // ensure BaseDirectory is set
            var baseDirectory = config["BaseDirectory"]
                ?? throw new ArgumentException("Required BaseDirectory option is not set!");

            if (!Directory.Exists(baseDirectory))
                throw new ArgumentException("BaseDirectory does not exist!");
        }

        private static void ApplyMigrations(WebApplication app)
        {
            try
            {
                // Migrations are associated with PostgresDatabaseContext (see
                // PostgresDatabaseContextFactory), so they must be applied through it.
                // It reads the connection string from configuration.
                using var dbContext = new PostgresDatabaseContext(app.Configuration);
                dbContext.Database.Migrate();
                app.Logger.LogInformation("Database schema is up to date.");
            }
            catch (Exception ex)
            {
                app.Logger.LogCritical(ex, "Failed to apply database migrations. Is the database reachable and is the connection string correct?");
                Environment.Exit(-1);
            }
        }
    }
}