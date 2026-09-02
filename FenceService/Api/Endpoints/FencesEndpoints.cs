using Application;

namespace Api.Endpoints;

public static class FencesEndpoints
{
    public static IEndpointRouteBuilder MapCreateFence(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/fences", (
            CreateFenceCommand command,
            CreateFenceCommandHandler handler) =>
        {
            var result = handler.Handle(command);
            return result.IsSuccess
                ? Results.Created($"/api/fences/{result.Value.Value}", new
                {
                    id = result.Value.Value
                })
                : Results.BadRequest(result.Errors);
        });
        return app;
    }
}