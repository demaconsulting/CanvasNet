namespace DemaConsulting.CanvasNet.Layout.Tests.Dagre;

/// <summary>
///     The failure the test meter (<see cref="TestLayoutWorkMeter"/>) creates, standing in for the located exceptions of
///     a diagram package.
/// </summary>
/// <remarks>
///     It derives from <see cref="InvalidOperationException"/> like the diagram packages' render exceptions, so the
///     tests prove that the layout lets a consumer failure of that type pass instead of wrapping it.
/// </remarks>
public sealed class LayoutTestException : InvalidOperationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LayoutTestException"/> class as a render failure.
    /// </summary>
    public LayoutTestException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="LayoutTestException"/> class as a render failure with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public LayoutTestException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="LayoutTestException"/> class as a render failure with a message
    ///     and an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public LayoutTestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="LayoutTestException"/> class.
    /// </summary>
    /// <param name="isLimit">Whether the failure is a limit failure (else a render failure).</param>
    /// <param name="message">The error message.</param>
    /// <param name="maxWork">The work budget of the meter that created the failure.</param>
    public LayoutTestException(bool isLimit, string message, long maxWork)
        : base(message)
    {
        IsLimit = isLimit;
        MaxWork = maxWork;
    }

    /// <summary>
    ///     Gets a value indicating whether the failure is a limit failure (else a render failure).
    /// </summary>
    public bool IsLimit { get; }

    /// <summary>
    ///     Gets the work budget of the meter that created the failure (zero when unknown).
    /// </summary>
    public long MaxWork { get; }
}
