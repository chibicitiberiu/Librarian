using System.Text.RegularExpressions;
using Xunit;

namespace Test
{
    /// <summary>
    /// The naming signals that turn a folder tree into a Show ⊃ Season hierarchy (collection_plan.md §6.3).
    /// These mirror the private patterns in ItemAssociationService; kept in sync by intent. They guard the
    /// boundary cases (what counts as a structural/season level vs ordinary content folders).
    /// </summary>
    public class StructuralNameTests
    {
        private static readonly Regex StructuralName = new(
            @"^(season|series|saison|book|vol|volume|disc|disk|cd|part|pt|s)[\s._-]*\d+$|^(specials?|extras?|bonus)$",
            RegexOptions.IgnoreCase);

        private static readonly Regex SeasonName = new(
            @"^(season|series|saison|s)[\s._-]*\d+$", RegexOptions.IgnoreCase);

        private static bool IsStructural(string n) => StructuralName.IsMatch(n.Trim());
        private static bool IsSeason(string n) =>
            SeasonName.IsMatch(n.Trim()) || n.Trim().ToLowerInvariant() is "specials" or "special" or "extras";

        [Theory]
        [InlineData("Season 1")]
        [InlineData("Season 1960")]
        [InlineData("season 02")]
        [InlineData("S01")]
        [InlineData("Disc 2")]
        [InlineData("Vol.3")]
        [InlineData("Volume 12")]
        [InlineData("Part_4")]
        [InlineData("Specials")]
        [InlineData("Extras")]
        public void Structural_names_match(string n) => Assert.True(IsStructural(n));

        [Theory]
        [InlineData("Tom and Jerry")]
        [InlineData("normalized")]
        [InlineData("The Beatles")]
        [InlineData("Season Finale")]   // "Season" with no number is not a level
        [InlineData("Disc Golf")]
        public void Ordinary_names_do_not_match(string n) => Assert.False(IsStructural(n));

        [Theory]
        [InlineData("Season 1960", true)]
        [InlineData("S03", true)]
        [InlineData("Specials", true)]
        [InlineData("Disc 2", false)]    // a disc is structural but not a Season
        [InlineData("Vol.3", false)]
        public void Season_detection(string n, bool expected) => Assert.Equal(expected, IsSeason(n));
    }
}
