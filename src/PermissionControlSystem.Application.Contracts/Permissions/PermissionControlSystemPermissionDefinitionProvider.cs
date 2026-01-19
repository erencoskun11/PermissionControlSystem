using PermissionControlSystem.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace PermissionControlSystem.Permissions;

public class PermissionControlSystemPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(PermissionControlSystemPermissions.GroupName);
        //Define your own permissions here. Example:
        //myGroup.AddPermission(PermissionControlSystemPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<PermissionControlSystemResource>(name);
    }
}
