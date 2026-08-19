using Application.Abstractions;
using Domain.ValueObjects;
using WhereUAt.SharedKernel;

namespace Infrastructure;

public class PermissionService : IPermissionService
{
    public Result CanWatch(CreatorId creatorId, TargetId targetId)
    {
        // TODO - Implement this
        return Result.Success();
    }
}