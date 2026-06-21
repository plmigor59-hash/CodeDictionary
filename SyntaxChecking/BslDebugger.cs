using ScriptEngine.Machine;
using ScriptEngine.Machine.Debugger;

namespace CodeDictionary.SyntaxChecking
{
    public class BslDebugger : IDebugger, IDebugSession, IThreadEventsListener, IBreakpointManager, IDisposable
    {
        private readonly ManualResetEventSlim _pauseEvent = new(true);
        private readonly Dictionary<string, List<(int Line, string Condition)>> _breakpoints = new();
        private MachineInstance? _machine;
        private int _stoppedThreadId;
        private MachineStopReason _stopReason;
        private string _errorMessage = string.Empty;

        public event Action<int, MachineStopReason, string>? BreakpointHit;
        public event Action? ExecutionFinished;

        public bool IsEnabled { get; set; } = true;

        public void Start() { }

        public IDebugSession GetSession() => this;

        public void NotifyProcessExit(int exitCode)
        {
            ExecutionFinished?.Invoke();
        }

        public void Stop()
        {
            IsEnabled = false;
            _pauseEvent.Set();
        }

        IBreakpointManager IDebugSession.BreakpointManager => this;
        IThreadEventsListener IDebugSession.ThreadManager => this;

        void IDebugSession.WaitReadyToRun() { }
        bool IDebugSession.IsActive => true;

        void IDisposable.Dispose()
        {
            _pauseEvent?.Dispose();
            GC.SuppressFinalize(this);
        }

        public void SetBreakpoints(string module, (int Line, string Condition)[] breakpoints)
        {
            _breakpoints[module] = new List<(int, string)>(breakpoints);
        }

        public bool FindBreakpoint(string module, int line)
        {
            foreach (var kvp in _breakpoints)
            {
                foreach (var bp in kvp.Value)
                {
                    if (bp.Line == line)
                        return true;
                }
            }
            return false;
        }

        public string GetCondition(string module, int line) => string.Empty;

        public void Clear()
        {
            _breakpoints.Clear();
        }

        public void SetExceptionBreakpoints((string Id, string Condition)[] filters) { }
        public bool StopOnAnyException(string message) => false;
        public bool StopOnUncaughtException(string message) => false;

        public void ThreadStarted(int threadId, MachineInstance machine)
        {
            _machine = machine;
        }

        public void ThreadStopped(int threadId, MachineStopReason reason, string errorMessage)
        {
            _pauseEvent.Reset();
            _stoppedThreadId = threadId;
            _stopReason = reason;
            _errorMessage = errorMessage ?? string.Empty;
            BreakpointHit?.Invoke(threadId, reason, _errorMessage);
            _pauseEvent.Wait();
        }

        public void ThreadExited(int threadId)
        {
            ExecutionFinished?.Invoke();
        }

        public void Resume()
        {
            _pauseEvent.Set();
        }

        public void StepOver()
        {
            if (_machine == null) return;
            _machine.StepOver();
            _pauseEvent.Set();
        }

        public void StepIn()
        {
            if (_machine == null) return;
            _machine.StepIn();
            _pauseEvent.Set();
        }

        public void StepOut()
        {
            if (_machine == null) return;
            _machine.StepOut();
            _pauseEvent.Set();
        }

        public int StoppedLineNumber
        {
            get
            {
                if (_machine == null)
                    return -1;
                try
                {
                    var frames = _machine.GetExecutionFrames();
                    if (frames.Count > 0)
                        return frames[0].LineNumber;
                }
                catch { }
                return -1;
            }
        }

        public string? Evaluate(string expression)
        {
            if (_machine == null)
                return null;
            try
            {
                var result = _machine.Evaluate(expression);
                return result?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                return $"<{ex.Message}>";
            }
        }
    }
}
