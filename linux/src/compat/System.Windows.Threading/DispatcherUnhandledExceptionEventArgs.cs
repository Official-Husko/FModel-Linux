namespace System.Windows.Threading;

public class DispatcherUnhandledExceptionEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
    public bool Handled { get; set; }
}
