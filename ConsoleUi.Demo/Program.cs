using ConsoleUi.Core.Logging;
using ConsoleUi.Demo.Logging;

await using AsyncPipeLogger log = await AsyncPipeLogger.ConnectAsync(PipeConstants.PipeName);

log.Info("App", "Renderer started");

for (int frame = 1; frame <= 300; frame++)
{
    log.Debug("Render", $"Frame {frame}");
    await Task.Delay(10);
}

log.Warn("App", "Renderer exiting");
Console.ReadLine();