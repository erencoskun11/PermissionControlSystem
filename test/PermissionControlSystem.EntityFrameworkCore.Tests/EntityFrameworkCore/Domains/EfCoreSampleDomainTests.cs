using PermissionControlSystem.Samples;
using Xunit;

namespace PermissionControlSystem.EntityFrameworkCore.Domains;

[Collection(PermissionControlSystemTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<PermissionControlSystemEntityFrameworkCoreTestModule>
{

}
