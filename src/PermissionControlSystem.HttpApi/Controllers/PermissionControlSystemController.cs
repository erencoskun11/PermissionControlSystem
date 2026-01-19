using PermissionControlSystem.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace PermissionControlSystem.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class PermissionControlSystemController : AbpControllerBase
{
    protected PermissionControlSystemController()
    {
        LocalizationResource = typeof(PermissionControlSystemResource);
    }
}
