using Lanpartyseating.Desktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace Lanpartyseating.Desktop.Business;

public class Timekeeper : IDisposable
{
    private readonly ILogger _logger;
    private readonly ISessionManager _sessionManager;
    private readonly ITrayPipeService _trayPipeService;
    private readonly ReservationManager _reservationManager;
    private Timer? _sessionEndTimer;
    private CancellationTokenSource? _loginCts;
    private DateTimeOffset _sessionEndTime;
    private int _sessionGeneration;
    private readonly object _lock = new();
    private readonly Timer _10MinuteWarningTimer;
    private readonly Timer _2MinuteWarningTimer;

    public Timekeeper(ILogger<Timekeeper> logger,
        ISessionManager sessionManager,
        ITrayPipeService trayPipeService,
        ReservationManager reservationManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _trayPipeService = trayPipeService;
        _reservationManager = reservationManager;
        _10MinuteWarningTimer = new Timer(ShowMinuteWarning!, 10, Timeout.Infinite, Timeout.Infinite);
        _2MinuteWarningTimer = new Timer(ShowMinuteWarning!, 2, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task StartSessionAsync(DateTimeOffset startTime, DateTimeOffset endTime)
    {
        Timer? oldTimer;
        CancellationTokenSource? oldCts;
        int generation;
        var duration = endTime - DateTimeOffset.UtcNow;

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

            generation = ++_sessionGeneration;

            var _2MinutesBeforeEnd = endTime.AddMinutes(-2);
            var _10MinutesBeforeEnd = endTime.AddMinutes(-10);

            oldTimer = _sessionEndTimer;
            _sessionEndTimer = null;
            oldCts = _loginCts;
            _loginCts = new CancellationTokenSource();

            _sessionEndTime = endTime;
            _sessionEndTimer = new Timer(SessionEnded, generation, duration, Timeout.InfiniteTimeSpan);

            if (_2MinutesBeforeEnd > DateTimeOffset.UtcNow)
            {
                _2MinuteWarningTimer.Change(_2MinutesBeforeEnd - DateTimeOffset.UtcNow, Timeout.InfiniteTimeSpan);
            }
            if (_10MinutesBeforeEnd > DateTimeOffset.UtcNow)
            {
                _10MinuteWarningTimer.Change(_10MinutesBeforeEnd - DateTimeOffset.UtcNow, Timeout.InfiniteTimeSpan);
            }

            _reservationManager.StartReservation(startTime, endTime);

            _logger.LogInformation("Session started (gen {Generation}). Will end at {EndTime}", generation, endTime);
        }

        oldCts?.Cancel();
        oldCts?.Dispose();
        oldTimer?.Dispose();

        await _sessionManager.SignInGamerAccountAsync();

        lock (_lock)
        {
            _loginCts = null;
        }
    }

    public async Task ExtendSessionAsync(DateTimeOffset newEndTime)
    {
        int deltaMinutes = 0;
        int minutesUntilEnd = 0;
        bool canExtend = false;

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
                deltaMinutes = Convert.ToInt32((newEndTime - _sessionEndTime).TotalMinutes);
                _sessionEndTime = newEndTime;
                var duration = newEndTime - DateTimeOffset.UtcNow;
                minutesUntilEnd = Convert.ToInt32(duration.TotalMinutes);
                _sessionEndTimer?.Change(duration, Timeout.InfiniteTimeSpan);
                _reservationManager.ExtendReservation(newEndTime);
                _logger.LogInformation("Session extended. New end time: {NewEndTime}", newEndTime);
                canExtend = true;
            }
            else
            {
                _logger.LogInformation("New end time must be later than the current end time.");
            }
        }

        if (canExtend)
        {
            await _trayPipeService.SendMessageAsync(new TextMessage{ Content = $"Session extended by {deltaMinutes} minutes. Your session will end in {minutesUntilEnd} minutes." }, CancellationToken.None);
            _logger.LogInformation("Time extension message sent down pipe.");
        }
    }

    private async void ShowMinuteWarning(object? state)
    {
        var minutes = (int)state!;
        await _trayPipeService.SendMessageAsync(new TextMessage{ Content = $"Your session will end in {minutes} minutes." }, CancellationToken.None);
        _logger.LogInformation("Sent {Minutes} minute warning", minutes);
    }

    public void EndSession()
    {
        Timer? oldTimer;
        CancellationTokenSource? oldCts;

        lock (_lock)
        {
            oldTimer = _sessionEndTimer;
            _sessionEndTimer = null;
            oldCts = _loginCts;
            _loginCts = null;
            _sessionEndTime = DateTimeOffset.MinValue;
            _reservationManager.EndReservation();
            _logger.LogInformation("Session forcibly ended.");
        }

        oldCts?.Cancel();
        oldCts?.Dispose();
        oldTimer?.Dispose();

        _sessionManager.SignOut();
    }

    private void SessionEnded(object? state)
    {
        var generation = (int)state!;
        _logger.LogInformation("Session end timer fired (gen {Generation}).", generation);

        if (generation != Volatile.Read(ref _sessionGeneration))
        {
            _logger.LogInformation("Stale session end callback (gen {Gen}) — current gen is {CurrentGen}. Ignoring.",
                generation, _sessionGeneration);
            return;
        }

        lock (_lock)
        {
            if (generation != _sessionGeneration) return;
            if (!_reservationManager.IsReservationActive)
            {
                _logger.LogInformation("Session end timer fired but reservation is no longer active. Ignoring.");
                return;
            }
            if (_loginCts is not null)
            {
                _logger.LogInformation("Session end timer fired but login is still in progress. Ignoring.");
                return;
            }

            _sessionEndTimer?.Dispose();
            _sessionEndTimer = null;
            _sessionEndTime = DateTimeOffset.MinValue;
            _reservationManager.EndReservation();
        }
        _sessionManager.SignOut();
    }

    public void Dispose()
    {
        _loginCts?.Cancel();
        _loginCts?.Dispose();
        _sessionEndTimer?.Dispose();
        _10MinuteWarningTimer?.Dispose();
        _2MinuteWarningTimer?.Dispose();
    }
}
