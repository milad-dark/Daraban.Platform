using System.Data.Common;
using Daraban.Modules.Settings.Data;
using Xunit;

namespace Daraban.Modules.Settings.Tests;

/// <summary>
/// Catalog integrity (Task 7.4): the catalog is the single source of truth for the whole
/// feature -- the seeder creates rows from it, the API validates against it, the UI groups
/// by it. These tests pin the invariants every consumer relies on.
/// </summary>
public class SettingCatalogTests
{
    [Fact]
    public void Every_Key_Is_Unique()
    {
        var duplicates = SettingCatalog.All
            .GroupBy(d => d.Key, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Every_Key_Belongs_To_A_Known_Category()
    {
        Assert.All(SettingCatalog.All, d =>
            Assert.Contains(d.Category, SettingCategories.All));
    }

    [Fact]
    public void Every_Category_Has_At_Least_One_Setting()
    {
        Assert.All(SettingCategories.All, category =>
            Assert.Contains(SettingCatalog.All, d => d.Category == category));
    }

    [Fact]
    public void Keys_Use_The_Dotted_Category_Prefix()
    {
        // The frontend groups and the test endpoint routes by this convention; a key that
        // breaks it would silently disappear from its category tab.
        Assert.All(SettingCatalog.All, d =>
            Assert.StartsWith(d.Category + ".", d.Key, StringComparison.Ordinal));
    }

    [Fact]
    public void Secrets_Are_Only_Password_Or_Credential_Keys()
    {
        // Guard against a future entry flipping IsSecret accidentally (a masked
        // application name would be unusable; an unmasked password would leak).
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "email.smtp_password",
            "ldap.bind_password",
        };

        Assert.Equal(expected, SettingCatalog.All.Where(d => d.IsSecret).Select(d => d.Key).ToHashSet());
    }

    [Fact]
    public void Defaults_Pass_Their_Own_Validation()
    {
        // A default the validator rejects would make the shipped value un-editable without
        // first being "fixed" by an admin -- catch it here instead.
        Assert.All(SettingCatalog.All, d =>
        {
            if (d.Default.Length == 0)
            {
                return; // empty is valid for optional settings
            }

            var error = Services.SettingValueValidator.Validate(d.Key, d.Default);
            Assert.True(error is null, $"Default for '{d.Key}' fails validation: {error?.Message}");
        });
    }

    [Fact]
    public void SeedSql_Contains_Every_Catalog_Row()
    {
        var sql = SettingCatalog.BuildSeedSql();

        Assert.All(SettingCatalog.All, d => Assert.Contains($"'{d.Key}'", sql, StringComparison.Ordinal));
    }

    [Fact]
    public void SeedSql_Escapes_Single_Quotes_In_Text()
    {
        // The description fields are embedded as literals; a future description with an
        // apostrophe must not produce broken SQL.
        var sql = SettingCatalog.BuildSeedSql();
        var selectIndex = sql.IndexOf("SELECT v.id", StringComparison.Ordinal);
        var insertSection = sql[selectIndex..];

        // No unmatched quote pairs per line: every literal line has an even quote count.
        var lines = insertSection.Split('\n');
        Assert.All(lines, line =>
        {
            if (line.Contains('\''))
            {
                Assert.Equal(0, line.Count(c => c == '\'') % 2);
            }
        });
    }

    [Fact]
    public void SeedSql_Uses_OnConflict_Do_Nothing_To_Preserve_Operator_Values()
    {
        var sql = SettingCatalog.BuildSeedSql();

        Assert.Contains("ON CONFLICT (key) DO NOTHING", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SeedSql_Creates_The_Unique_Key_Index()
    {
        var sql = SettingCatalog.BuildSeedSql();

        Assert.Contains("CREATE UNIQUE INDEX IF NOT EXISTS uq_system_settings_key", sql, StringComparison.Ordinal);
    }
}
