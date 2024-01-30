using Lanpartyseating.Desktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace Lanpartyseating.Desktop.Business;

public class Timekeeper : IDisposable
{
    private readonly ILogger _logger;
    private readonly ISessionManager _sessionManager;
    private readonly INamedPipeServerService _pipeServer;
    private readonly ReservationManager _reservationManager;
    private readonly Timer _timer;
    private DateTimeOffset _sessionEndTime;
    private readonly object _lock = new();
    private readonly Timer _10MinuteWarningTimer;
    private readonly Timer _2MinuteWarningTimer;

    public Timekeeper(ILogger<Timekeeper> logger,
        ISessionManager sessionManager,
        INamedPipeServerService pipeServer,
        ReservationManager reservationManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _pipeServer = pipeServer;
        _reservationManager = reservationManager;
        _timer = new Timer(SessionEnded!, null, Timeout.Infinite, Timeout.Infinite);
        _10MinuteWarningTimer = new Timer(ShowMinuteWarning!, 10, Timeout.Infinite, Timeout.Infinite);
        _2MinuteWarningTimer = new Timer(ShowMinuteWarning!, 2, Timeout.Infinite, Timeout.Infinite);
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
            var _2MinutesBeforeEnd = endTime.AddMinutes(-2);
            var _10MinutesBeforeEnd = endTime.AddMinutes(-10);
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
            
            if (_2MinutesBeforeEnd > DateTimeOffset.UtcNow)
            {
                _2MinuteWarningTimer.Change(_2MinutesBeforeEnd - DateTimeOffset.UtcNow, Timeout.InfiniteTimeSpan);
            }
            if (_10MinutesBeforeEnd > DateTimeOffset.UtcNow)
            {
                _10MinuteWarningTimer.Change(_10MinutesBeforeEnd - DateTimeOffset.UtcNow, Timeout.InfiniteTimeSpan);
            }
            
            _reservationManager.StartReservation(startTime, endTime);

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
                var _2MinutesBeforeEnd = newEndTime.AddMinutes(-2);
                var _10MinutesBeforeEnd = newEndTime.AddMinutes(-10);
                if (_2MinutesBeforeEnd > DateTimeOffset.UtcNow)
                {
                    _2MinuteWarningTimer.Change(_2MinutesBeforeEnd - DateTimeOffset.UtcNow, Timeout.InfiniteTimeSpan);
                }
                if (_10MinutesBeforeEnd > DateTimeOffset.UtcNow)
                {
                    _10MinuteWarningTimer.Change(_10MinutesBeforeEnd - DateTimeOffset.UtcNow, Timeout.InfiniteTimeSpan);
                }
                var deltaMinutes = Convert.ToInt32((newEndTime - _sessionEndTime).TotalMinutes);
                _sessionEndTime = newEndTime;
                var duration = newEndTime - DateTimeOffset.UtcNow;
                var minutesUntilEnd = Convert.ToInt32(duration.TotalMinutes);
                _timer.Change(duration, Timeout.InfiniteTimeSpan);
                _reservationManager.ExtendReservation(newEndTime);
                _logger.LogInformation($"Session extended. New end time: {newEndTime}.");
                _pipeServer.SendMessageAsync(new TextMessage{ Content = $"Session extended by {deltaMinutes} minutes. Your session will end in {minutesUntilEnd} minutes." }, CancellationToken.None).Wait();
                _logger.LogInformation("Time extension message sent down pipe.");
            }
            else
            {
                _logger.LogInformation("New end time must be later than the current end time.");
            }
        }
    }
    
    private void ShowMinuteWarning(object? state)
    {
        var minutes = (int) state!;
        _pipeServer.SendMessageAsync(new TextMessage{ Content = $"Your session will end in {minutes} minutes." }, CancellationToken.None).Wait();
        _logger.LogInformation($"Sent {minutes} minute warning.");
    }

    public void EndSession()
    {
        lock (_lock)
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _sessionEndTime = DateTimeOffset.MinValue;
            _reservationManager.EndReservation();
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