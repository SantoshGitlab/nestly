using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Identity.Tests;

/// <summary>
/// The Phase 6 permission matrix itself (SRS 12.2.3, task 96a) — invariants
/// that must hold regardless of how the module/role lists are edited in the
/// future, so a typo in <see cref="AdminPermissionCatalog"/> fails a test
/// instead of silently shipping a broken grant.
/// </summary>
public class AdminPermissionCatalogTests
{
    [Fact]
    public void Every_module_has_exactly_a_read_and_a_write_permission()
    {
        AdminPermissionCatalog.Permissions.Should().HaveCount(AdminModules.All.Count * 2);

        foreach (string module in AdminModules.All)
        {
            AdminPermissionCatalog.Permissions
                .Count(p => p.Module == module && p.Action == AdminPermissionAction.Read)
                .Should().Be(1, $"{module} should have exactly one read permission");
            AdminPermissionCatalog.Permissions
                .Count(p => p.Module == module && p.Action == AdminPermissionAction.Write)
                .Should().Be(1, $"{module} should have exactly one write permission");
        }
    }

    [Fact]
    public void Permission_codes_are_unique()
    {
        AdminPermissionCatalog.Permissions.Select(p => p.Code).Distinct()
            .Should().HaveCount(AdminPermissionCatalog.Permissions.Count);
    }

    [Theory]
    [InlineData(AdminPermissionAction.Read, "read")]
    [InlineData(AdminPermissionAction.Write, "write")]
    public void BuildCode_appends_the_action_suffix(AdminPermissionAction action, string suffix)
    {
        AdminPermissionCatalog.BuildCode("catalog", action).Should().Be($"catalog.{suffix}");
    }

    [Fact]
    public void Every_role_named_in_SRS_12_2_2_has_a_grant()
    {
        AdminPermissionCatalog.RolePermissionCodes.Keys.Should().BeEquivalentTo(AdminRoleNames.All);
    }

    [Fact]
    public void Every_granted_code_actually_exists_in_the_catalog()
    {
        var validCodes = AdminPermissionCatalog.Permissions.Select(p => p.Code).ToHashSet();

        foreach ((string role, IReadOnlySet<string> codes) in AdminPermissionCatalog.RolePermissionCodes)
        {
            codes.Should().NotBeEmpty($"{role} should hold at least one permission");
            foreach (string code in codes)
            {
                validCodes.Should().Contain(code, $"{role} grants unknown code {code}");
            }
        }
    }

    [Fact]
    public void Super_admin_holds_every_permission_in_the_matrix()
    {
        var allCodes = AdminPermissionCatalog.Permissions.Select(p => p.Code).ToHashSet();

        AdminPermissionCatalog.RolePermissionCodes[AdminRoleNames.SuperAdmin].Should().BeEquivalentTo(allCodes);
    }

    [Fact]
    public void Read_only_analyst_can_read_everything_and_write_nothing()
    {
        IReadOnlySet<string> analystCodes = AdminPermissionCatalog.RolePermissionCodes[AdminRoleNames.ReadOnlyAnalyst];

        foreach (string module in AdminModules.All)
        {
            analystCodes.Should().Contain(AdminPermissionCatalog.BuildCode(module, AdminPermissionAction.Read));
            analystCodes.Should().NotContain(AdminPermissionCatalog.BuildCode(module, AdminPermissionAction.Write));
        }
    }

    [Fact]
    public void A_role_granted_write_on_a_module_is_also_granted_read_on_it()
    {
        foreach ((string role, IReadOnlySet<string> codes) in AdminPermissionCatalog.RolePermissionCodes)
        {
            foreach (string module in AdminModules.All)
            {
                string writeCode = AdminPermissionCatalog.BuildCode(module, AdminPermissionAction.Write);
                string readCode = AdminPermissionCatalog.BuildCode(module, AdminPermissionAction.Read);

                if (codes.Contains(writeCode))
                {
                    codes.Should().Contain(readCode, $"{role} can write {module} but not read it");
                }
            }
        }
    }

    [Fact]
    public void Every_seeded_role_has_a_description()
    {
        AdminRoleNames.Descriptions.Keys.Should().BeEquivalentTo(AdminRoleNames.All);
        AdminRoleNames.Descriptions.Values.Should().OnlyContain(d => !string.IsNullOrWhiteSpace(d));
    }
}
