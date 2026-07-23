using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace SafeRide.API.Swagger;

public sealed class HideSendChatImageApiDescriptionProvider : IApiDescriptionProvider
{
    public int Order => 1000;

    public void OnProvidersExecuting(ApiDescriptionProviderContext context)
    {
    }

    public void OnProvidersExecuted(ApiDescriptionProviderContext context)
    {
        for (var index = context.Results.Count - 1; index >= 0; index--)
        {
            var description = context.Results[index];

            if (string.Equals(
                    description.ActionDescriptor.RouteValues["controller"],
                    "Trips",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    description.ActionDescriptor.RouteValues["action"],
                    "SendChatImage",
                    StringComparison.OrdinalIgnoreCase))
            {
                context.Results.RemoveAt(index);
            }
        }
    }
}
