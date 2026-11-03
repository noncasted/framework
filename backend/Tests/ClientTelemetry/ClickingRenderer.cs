using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging.Abstractions;

namespace Framework.Tests.ClientTelemetry;

#pragma warning disable BL0006 // the render tree is the only way to reach a handler without a browser

// Renders a page and presses its first button: the static HtmlRenderer has no events.
public sealed class ClickingRenderer : Renderer
{
    public ClickingRenderer(IServiceProvider services) : base(services, NullLoggerFactory.Instance)
    {
    }

    private readonly List<ulong> _clicks = [];

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    public Task RenderPage(Type page, Dictionary<string, object?> parameters)
    {
        return Dispatcher.InvokeAsync(() =>
        {
            var id = AssignRootComponentId(InstantiateComponent(page));
            return RenderRootComponentAsync(id, ParameterView.FromDictionary(parameters));
        });
    }

    public Task ClickFirstButton()
    {
        return Dispatcher.InvokeAsync(() => DispatchEventAsync(_clicks[0], null, new MouseEventArgs()));
    }

    protected override void HandleException(Exception exception)
    {
        throw exception;
    }

    protected override Task UpdateDisplayAsync(in RenderBatch batch)
    {
        for (var index = 0; index < batch.ReferenceFrames.Count; index++)
        {
            var frame = batch.ReferenceFrames.Array[index];

            if (frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "onclick" && frame.AttributeEventHandlerId != 0)
            {
                _clicks.Add(frame.AttributeEventHandlerId);
            }
        }

        return Task.CompletedTask;
    }
}
