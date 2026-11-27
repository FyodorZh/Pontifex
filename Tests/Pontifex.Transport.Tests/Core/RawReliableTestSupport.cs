using System.Collections.Concurrent;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;

namespace Pontifex.Tests;

internal static class RawReliableTestSupport
{
    public static TaskCompletionSource CreateCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static TaskCompletionSource<T> CreateCompletionSource<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static async Task StopClientAsync(
        IRawReliableClient client,
        TaskCompletionSource<StopReason> stopped,
        TimeSpan timeout,
        ConcurrentBag<string> errors,
        string context)
    {
        try
        {
            client.Stop(new UserIntention("test", "Test cleanup"));
            var reason = await stopped.Task.WaitAsync(timeout);
            if (reason is AnyFail)
            {
                errors.Add($"{context}: client cleanup stopped with {reason}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"{context}: client cleanup failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static async Task StopServerAsync(
        IRawReliableServer server,
        TaskCompletionSource<StopReason> stopped,
        TrackingServerHandlerFactory? sessions,
        TimeSpan timeout,
        ConcurrentBag<string> errors,
        string context)
    {
        try
        {
            server.Stop(new UserIntention("test", "Test cleanup"));
            var reason = await stopped.Task.WaitAsync(timeout);
            if (reason is AnyFail)
            {
                errors.Add($"{context}: server cleanup stopped with {reason}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"{context}: server cleanup failed: {ex.GetType().Name}: {ex.Message}");
        }

        if (sessions == null)
        {
            return;
        }

        try
        {
            await sessions.WaitForAllStoppedAsync(timeout);
        }
        catch (Exception ex)
        {
            errors.Add($"{context}: server sessions did not stop: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
