using ConsoleUi.Core.Logging;
using ConsoleUi.Demo.Logging;

await using var logger = await AsyncPipeLogger.ConnectAsync(PipeConstants.PipeName);

CategoryLogger render = logger.For("Render");
CategoryLogger layout = logger.For("Layout");

render.Info("Renderer started");

for (int i = 0; i < 100; i++)
{
    render.Debug($"Frame {i}");
    await Task.Delay(30);
}

layout.Warn("Layout recalculated");