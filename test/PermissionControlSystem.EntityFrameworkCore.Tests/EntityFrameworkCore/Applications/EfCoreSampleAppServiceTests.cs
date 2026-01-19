using PermissionControlSystem.Samples;
using Xunit;

namespace PermissionControlSystem.EntityFrameworkCore.Applications;

[Collection(PermissionControlSystemTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<PermissionControlSystemEntityFrameworkCoreTestModule>
{

}
