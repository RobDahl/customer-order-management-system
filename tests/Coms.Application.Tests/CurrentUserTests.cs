using Coms.Application.Tests.Fakes;
using Xunit;

namespace Coms.Application.Tests
{
    public class CurrentUserTests
    {
        [Fact]
        public void FakeCurrentUser_ExposesConfiguredName()
        {
            ICurrentUser user = new FakeCurrentUser("clerk");

            Assert.Equal("clerk", user.UserName);
        }
    }
}
