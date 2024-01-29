using Microsoft.Extensions.Logging;

namespace Lanpartyseating.Desktop.Business;

using System;
using System.Threading;

public class Timekeeper : IDisposable
{
    private readonly ILogger _logger;
    private readonly ISessionManager _sessionManager;
    private Timer _timer;
    private DateTimeOffset _sessionEndTime;
    private readonly object _lock = new();

    public Timekeeper(ILogger<Timekeeper> logger, ISessionManager sessionManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _timer = new Timer(SessionEnded, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void StartSession(DateTimeOffset startTime, DateTimeOffset endTime)
    {
        lock (_lock)
        {
            if (endTime <= startTime)
            {
                throw new ArgumentException("End time must be later than start time.");
            }
            
            if (endTime <= DateTimeOffset.UtcNow)
            {
                throw new ArgumentException("End time must be in the future.");
            }

            _sessionEndTime = endTime;
            var duration = endTime - DateTimeOffset.UtcNow;

            // If the start time is in the future, delay the timer start
            if (startTime > DateTimeOffset.UtcNow)
            {
                _timer.Change(startTime - DateTimeOffset.UtcNow, Timeout.InfiniteTimeSpan);
            }
            else
            {
                _timer.Change(duration, Timeout.InfiniteTimeSpan);
            }

            _logger.LogInformation($"Session started. Will end at {endTime}.");
            _sessionManager.SignInGamerAccount();
        }
    }

    public void ExtendSession(DateTimeOffset newEndTime)
    {
        lock (_lock)
        {
            if (newEndTime <= DateTimeOffset.UtcNow)
            {
                throw new ArgumentException("New end time must be in the future.");
            }

            if (newEndTime > _sessionEndTime)
            {
                _sessionEndTime = newEndTime;
                var duration = newEndTime - DateTimeOffset.UtcNow;
                _timer.Change(duration, Timeout.InfiniteTimeSpan);
                _logger.LogInformation($"Session extended. New end time: {newEndTime}.");
            }
            else
            {
                _logger.LogInformation("New end time must be later than the current end time.");
            }
        }
    }

    public void EndSession()
    {
        lock (_lock)
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _sessionEndTime = DateTimeOffset.MinValue;
            _logger.LogInformation("Session forcibly ended.");
            _sessionManager.SignOut();
        }
    }

    private void SessionEnded(object state)
    {
        _logger.LogInformation("Session ended.");
        _sessionManager.SignOut();
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}