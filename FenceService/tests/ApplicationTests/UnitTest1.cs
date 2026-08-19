using Application;
using Infrastructure;

namespace ApplicationTests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        var command = new CreateFenceCommand(
            "FenceName",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            10,
            5,
            7
        );

        var handler = new CreateFenceCommandHandler(new PermissionService(), new EventStore());

        handler.Handle(command);
    }
}