using System;
using System.Collections.Generic;

namespace Librarian.ViewModels
{
    /// <summary>A group of properties shown in the viewer's right-hand pane.</summary>
    public class WmPropGroup
    {
        public WmPropGroup(string group, IReadOnlyList<(string Name, string Value)> items)
        {
            Group = group;
            Items = items;
        }

        public string Group { get; }
        public IReadOnlyList<(string Name, string Value)> Items { get; }
    }

    /// <summary>Backing model for the in-window viewer: a preview (chosen by MIME) plus the file's
    /// collected metadata as properties.</summary>
    public class WmViewerViewModel
    {
        public string DisplayName { get; set; } = "";
        public string Path { get; set; } = "";
        public string? ParentPath { get; set; }
        public IReadOnlyList<(string Name, string Path)> Breadcrumbs { get; set; } = Array.Empty<(string, string)>();

        /// <summary>image | audio | video | pdf | text | none — selects the preview widget.</summary>
        public string PreviewKind { get; set; } = "none";

        /// <summary>URL serving the raw file bytes inline (the Browse controller) — "View Raw".</summary>
        public string PreviewUrl { get; set; } = "";

        /// <summary>URL serving the raw file bytes as an attachment (Content-Disposition: attachment) — "Download".</summary>
        public string DownloadUrl { get; set; } = "";

        /// <summary>First chunk of a text file, for the text preview.</summary>
        public string? TextPreview { get; set; }

        public string MimeType { get; set; } = "";
        public string DisplaySize { get; set; } = "";
        public string Modified { get; set; } = "";
        public string IconUrl { get; set; } = "";

        /// <summary>Link to the full metadata editor (existing chrome) for edits.</summary>
        public string FullMetadataUrl { get; set; } = "";

        public IReadOnlyList<WmPropGroup> Properties { get; set; } = Array.Empty<WmPropGroup>();
    }
}
