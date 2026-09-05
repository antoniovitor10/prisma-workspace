using Detran.Kanban.Domain.Enums;

namespace Detran.Kanban.Domain.Authorization;

/// <summary>Matriz padrão dos perfis, complementada por concessões específicas.</summary>
public static class RolePermissionCatalog
{
    public static bool Allows(OrganizationRole role, PlatformPermission permission, PermissionScope scope)
    {
        if (role == OrganizationRole.Administrator)
            return true;

        if (role == OrganizationRole.ExternalRequester)
            return scope is PermissionScope.Request or PermissionScope.Form
                && permission is PlatformPermission.View or PlatformPermission.Create or PlatformPermission.Comment;

        if (role == OrganizationRole.Client)
            return scope is PermissionScope.Project or PermissionScope.WorkItem or PermissionScope.Request or PermissionScope.Report
                && permission is PlatformPermission.View or PlatformPermission.Comment or PlatformPermission.ViewReport;

        if (role == OrganizationRole.Viewer)
            return permission is PlatformPermission.View or PlatformPermission.ViewReport;

        return role switch
        {
            OrganizationRole.Manager => permission is not PlatformPermission.AdministerOrganization
                and not PlatformPermission.ManagePermissions,
            OrganizationRole.ProjectManager => permission is PlatformPermission.View
                or PlatformPermission.Create or PlatformPermission.Edit or PlatformPermission.Delete
                or PlatformPermission.Assign or PlatformPermission.ChangeStatus or PlatformPermission.Comment
                or PlatformPermission.RespondToRequester or PlatformPermission.ManageSprint
                or PlatformPermission.ViewReport or PlatformPermission.CreateReport,
            OrganizationRole.ScrumMaster => permission is PlatformPermission.View
                or PlatformPermission.Create or PlatformPermission.Edit or PlatformPermission.Assign
                or PlatformPermission.ChangeStatus or PlatformPermission.Comment
                or PlatformPermission.ManageSprint or PlatformPermission.ViewReport,
            OrganizationRole.ProductOwner => permission is PlatformPermission.View
                or PlatformPermission.Create or PlatformPermission.Edit or PlatformPermission.Assign
                or PlatformPermission.ChangeStatus or PlatformPermission.Comment
                or PlatformPermission.ManageSprint or PlatformPermission.ViewReport,
            OrganizationRole.TeamMember or OrganizationRole.Developer => permission is PlatformPermission.View
                or PlatformPermission.Create or PlatformPermission.Edit
                or PlatformPermission.ChangeStatus or PlatformPermission.Comment,
            _ => false
        };
    }
}
