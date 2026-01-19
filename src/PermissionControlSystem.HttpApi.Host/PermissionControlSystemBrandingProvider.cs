using Microsoft.Extensions.Localization;
using PermissionControlSystem.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace PermissionControlSystem;

[Dependency(ReplaceServices = true)]
public class PermissionControlSystemBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<PermissionControlSystemResource> _localizer;

    public PermissionControlSystemBrandingProvider(IStringLocalizer<PermissionControlSystemResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
