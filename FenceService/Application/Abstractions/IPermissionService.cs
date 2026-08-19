using Domain.ValueObjects;
using WhereUAt.SharedKernel;

namespace Application.Abstractions;

public interface IPermissionService
{
    public Result CanWatch(CreatorId creatorId, TargetId targetId);
}