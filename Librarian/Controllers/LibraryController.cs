using System;
using System.Collections.Generic;
using System.Linq;
using Librarian.Library;
using Librarian.Models;
using Librarian.Services;
using Librarian.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MimeMapping;

namespace Librarian.Controllers
{
    /// <summary>
    /// Faceted ("category") browsing — the read-only counterpart of <see cref="BrowseController"/>.
    /// A category (Music, Video, …) contains named facet views ("By Artist", "By Year", …). Browsing
    /// the category lists its views as folders; opening a view drills its path level by level, just
    /// like a folder tree. The chosen drill values travel in the <c>p</c> query parameter so every
    /// link is a plain Tier-0 anchor (no JS needed).
    /// </summary>
    public class LibraryController : Controller
    {
        private const string FolderIconUrl = "~/icons/16/file-types/folder.png";

        private readonly ILogger<LibraryController> logger;
        private readonly LibraryService libraryService;

        public LibraryController(ILogger<LibraryController> logger, LibraryService libraryService)
        {
            this.logger = logger;
            this.libraryService = libraryService;
        }

        public IActionResult Index(string category, string? view, [FromQuery(Name = "p")] string[]? p)
        {
            var cat = LibraryCategories.Find(category);
            if (cat == null)
                return NotFound($"Unknown category '{category}'.");

            // Resolve the active view. A single-view category drills directly (no intermediate list).
            CategoryView? cv = null;
            if (!string.IsNullOrEmpty(view))
            {
                cv = cat.FindView(view);
                if (cv == null)
                    return NotFound($"Unknown view '{view}' in category '{category}'.");
            }
            else if (cat.Views.Count == 1)
            {
                cv = cat.Views[0];
            }

            try
            {
                return cv == null ? View(BuildViewListModel(cat)) : View(BuildDrillModel(cat, cv, p));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error computing library listing for {Category}/{View}", cat.Key, view);
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>Category root: the views shown as folders.</summary>
        private LibraryViewModel BuildViewListModel(LibraryCategory cat)
        {
            return new LibraryViewModel
            {
                Category = cat,
                Selected = Array.Empty<string>(),
                IsLeaf = false,
                LevelLabel = "View",
                ParentUrl = null,
                DisplayName = cat.DisplayName,
                LibraryUri = "library://" + cat.DisplayName,
                Breadcrumbs = new[] { (cat.DisplayName, CatUrl(cat.Key)) },
                Folders = cat.Views.Select(v => new LibraryFolderViewModel
                {
                    Name = v.DisplayName,
                    Count = libraryService.CountView(cat.Filter, v),
                    Url = ViewUrl(cat.Key, v.Key, Array.Empty<string>()),
                    IconUrl = Url.Content(FolderIconUrl),
                }).ToList(),
            };
        }

        /// <summary>Inside a view: the next level's folders, or the leaf files.</summary>
        private LibraryViewModel BuildDrillModel(LibraryCategory cat, CategoryView cv, string[]? p)
        {
            bool multiView = cat.Views.Count > 1;
            var selected = (p ?? Array.Empty<string>()).Take(cv.Path.Count).ToArray();
            var listing = libraryService.GetListing(cat.Filter, cv.Path, cv.LeafSortAttributeId, selected);

            int depth = Math.Min(selected.Length, cv.Path.Count);

            // Drill-up: a level back within the view; at the view root, back to the view list
            // (multi-view) or nowhere (single-view category).
            string? parentUrl = selected.Length > 0
                ? ViewUrl(cat.Key, cv.Key, selected[..^1])
                : (multiView ? CatUrl(cat.Key) : null);

            return new LibraryViewModel
            {
                Category = cat,
                View = cv,
                Selected = selected,
                IsLeaf = listing.IsLeaf,
                LevelLabel = listing.IsLeaf ? null : cv.Path[depth].Label,
                ParentUrl = parentUrl,
                DisplayName = selected.Length > 0 ? selected[^1] : (multiView ? cv.DisplayName : cat.DisplayName),
                LibraryUri = "library://" + string.Join("/",
                    new[] { cat.DisplayName }
                        .Concat(multiView ? new[] { cv.DisplayName } : Array.Empty<string>())
                        .Concat(selected)),
                Breadcrumbs = BuildBreadcrumbs(cat, cv, multiView, selected),
                Folders = listing.Folders.Select(f => new LibraryFolderViewModel
                {
                    Name = string.IsNullOrEmpty(f.Value) ? "(unknown)" : f.Value,
                    Count = f.Count,
                    Url = ViewUrl(cat.Key, cv.Key, selected.Append(f.Value)),
                    IconUrl = Url.Content(FolderIconUrl),
                }).ToList(),
                Files = listing.Files.Select(ToFileViewModel).ToList(),
            };
        }

        private List<(string, string)> BuildBreadcrumbs(LibraryCategory cat, CategoryView cv, bool multiView, string[] selected)
        {
            var crumbs = new List<(string, string)> { (cat.DisplayName, CatUrl(cat.Key)) };
            if (multiView)
                crumbs.Add((cv.DisplayName, ViewUrl(cat.Key, cv.Key, Array.Empty<string>())));
            for (int i = 0; i < selected.Length; i++)
                crumbs.Add((selected[i], ViewUrl(cat.Key, cv.Key, selected[..(i + 1)])));
            return crumbs;
        }

        private BrowseFileViewModel ToFileViewModel(LibraryFileEntry entry)
        {
            string name = NameOf(entry.Path);
            string mime = MimeUtility.GetMimeMapping(name);

            return new BrowseFileViewModel
            {
                Name = name,
                Path = entry.Path,
                IsDirectory = false,
                Size = entry.Size,
                DisplaySize = entry.Size.HasValue ? HumanizeUtils.HumanizeSize(entry.Size.Value) : null,
                LastModified = entry.Modified,
                MimeType = mime,
                IconUrl = Url.Content(IconMapping.GetIconUrl(name, mime)),
            };
        }

        // view is explicitly nulled so URL generation drops the ambient {view} of the current
        // request (otherwise "/library/music" links would inherit ".../by-artist").
        private string CatUrl(string categoryKey) =>
            Url.Action("Index", "Library", new { category = categoryKey, view = (string?)null })!;

        /// <summary>
        /// Builds <c>/library/{category}/{view}?p=v1&amp;p=v2</c>. Each value is escaped individually
        /// so slashes and other specials in attribute values survive the round-trip (route catch-alls
        /// would mangle them, hence the query parameter).
        /// </summary>
        private string ViewUrl(string categoryKey, string viewKey, IEnumerable<string> values)
        {
            string basePath = Url.Action("Index", "Library", new { category = categoryKey, view = viewKey })!;
            string qs = string.Join("&", values.Select(v => "p=" + Uri.EscapeDataString(v)));
            return qs.Length == 0 ? basePath : basePath + "?" + qs;
        }

        private static string NameOf(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash >= 0 && slash < path.Length - 1 ? path[(slash + 1)..] : path;
        }
    }
}
