using Edith.Database.Migrator;

namespace Edith.Database.Migrator.Tests;

public class SqlMigratorTests
{
    [Fact]
    public void Scripts_are_ordered_by_module_dependency()
    {
        var scripts = SqlMigrator.LoadScripts(includeDevSeed: false);

        var moduleSequence = scripts.Select(s => s.Module).Distinct().ToList();
        Assert.Equal(["core", "rules", "screening", "cases", "notifications"], moduleSequence);
    }

    [Fact]
    public void Every_module_in_the_ddl_has_at_least_one_script()
    {
        var modules = SqlMigrator.LoadScripts(includeDevSeed: false).Select(s => s.Module).ToHashSet();

        foreach (var module in SqlMigrator.ModuleOrder)
        {
            Assert.Contains(module, modules);
        }
    }

    [Fact]
    public void Dev_seed_is_excluded_unless_requested_and_always_runs_last()
    {
        var without = SqlMigrator.LoadScripts(includeDevSeed: false);
        var with = SqlMigrator.LoadScripts(includeDevSeed: true);

        Assert.DoesNotContain(without, s => s.Module.StartsWith("seed:", StringComparison.Ordinal));
        Assert.True(with.Count > without.Count);
        Assert.StartsWith("seed:", with[^1].Module, StringComparison.Ordinal);
    }

    [Fact]
    public void Checksum_ignores_line_ending_differences()
    {
        Assert.Equal(
            SqlMigrator.ComputeChecksum("CREATE TABLE a (id int);\r\nSELECT 1;\r\n"),
            SqlMigrator.ComputeChecksum("CREATE TABLE a (id int);\nSELECT 1;\n"));
    }

    [Fact]
    public void Checksum_detects_content_changes()
    {
        Assert.NotEqual(
            SqlMigrator.ComputeChecksum("CREATE TABLE a (id int);"),
            SqlMigrator.ComputeChecksum("CREATE TABLE a (id bigint);"));
    }

    [Fact]
    public void Ddl_defines_the_expected_37_tables()
    {
        // 6 core + 8 rules + 13 screening + 7 cases + 3 notifications, per MD_DOC/database_v2.0.md.
        var tableCount = SqlMigrator.LoadScripts(includeDevSeed: false)
            .Sum(s => CountOccurrences(s.Sql, "CREATE TABLE "));

        Assert.Equal(37, tableCount);
    }

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        for (var i = text.IndexOf(token, StringComparison.Ordinal); i >= 0; i = text.IndexOf(token, i + token.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
