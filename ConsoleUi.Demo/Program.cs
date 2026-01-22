using ConsoleUi.Core.Logging; // Shared constants (pipe name)
using ConsoleUi.Demo.Logging; // Logger + helper classes

// STEP 1: Connect to the logger program through a named pipe
//
// "await using" means:
// - wait until the connection is ready
// - automatically clean everything up when the program exits
await using var logger = await AsyncPipeLogger.ConnectAsync(PipeConstants.PipeName);


// STEP 2: Create category-based loggers
//
// Think of CategoryLogger like folders:
// - Render logs go into the "Render" folder
// - Layout logs go into the "Layout" folder
CategoryLogger render = logger.For("Render");
CategoryLogger layout = logger.For("Layout");

// STEP 3: Send an Info message
render.Info("Renderer started");

// STEP 4: Pretend we are rendering frames
for (int i = 0; i < 100; i++)
{
    // Debug messages are usually very detailed
    render.Debug($"Frame {i}");

    // Slow things down so logs are readable
    await Task.Delay(30);
}

// STEP 5: Send a warning from a different category
layout.Warn("Layout recalculated");