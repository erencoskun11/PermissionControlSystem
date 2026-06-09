using PermissionControlSystem.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace PermissionControlSystem.Controllers;

 
public abstract class PermissionControlSystemController : AbpControllerBase
{
    protected PermissionControlSystemController()
    {
        LocalizationResource = typeof(PermissionControlSystemResource);
    }
}
