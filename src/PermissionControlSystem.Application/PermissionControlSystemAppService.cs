using System;
using System.Collections.Generic;
using System.Text;
using PermissionControlSystem.Localization;
using Volo.Abp.Application.Services;

namespace PermissionControlSystem;

/* Inherit your application services from this class.
 */
public abstract class PermissionControlSystemAppService : ApplicationService
{
    protected PermissionControlSystemAppService()
    {
        LocalizationResource = typeof(PermissionControlSystemResource);
    }
}
