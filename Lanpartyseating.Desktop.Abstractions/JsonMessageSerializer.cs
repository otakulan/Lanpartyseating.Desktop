namespace Lanpartyseating.Desktop.Abstractions;

using System.Text.Json;

public static class JsonMessageSerializer
{
    public static T Deserialize<T>(string json) where T : BaseMessage
    {
        return JsonSerializer.Deserialize<T>(json);
    }

    public static string Serialize(BaseMessage message)
    {
        return JsonSerializer.Serialize(message);
    }
}