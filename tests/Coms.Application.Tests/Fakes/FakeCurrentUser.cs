namespace Coms.Application.Tests.Fakes
{
    internal sealed class FakeCurrentUser : ICurrentUser
    {
        public FakeCurrentUser(string userName)
        {
            UserName = userName;
        }

        public string UserName { get; }
    }
}
