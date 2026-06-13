namespace Lanpartyseating.Desktop.Abstractions;

using System.Text.Json;

public static class JsonMessageSerializer
{
    public static T Deserialize<T>(string json) where T : BaseMessage
    {
        return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException("Failed to deserialize JSON message");
    }

    public static string Serialize(BaseMessage message)
    {
        return JsonSerializer.Serialize(message);
    }
}