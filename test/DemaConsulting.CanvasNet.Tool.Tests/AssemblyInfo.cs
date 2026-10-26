namespace DemaConsulting.CanvasNet.Tool.Tests;

// Tests share Console state, so they must not run in parallel.
/// <summary>
/// Defines the Sequential test collection.
/// Tests in this collection are disabled from running in parallel to
/// prevent conflicts when sharing Console state.
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public sealed class SequentialCollection { }
