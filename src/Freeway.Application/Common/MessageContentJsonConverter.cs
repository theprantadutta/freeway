using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Freeway.Application.Common;

/// <summary>
/// Reads a chat message's content in either form the OpenAI API accepts: a plain
/// string, or an array of content parts such as [{"type":"text","text":"..."}].
///
/// SDKs and the frameworks built on them send the array form routinely, even for
/// text alone, and a gateway that only took strings would turn those into a 400 on
/// the first message. Text parts are joined by newlines. Anything else -- an image, an
/// audio clip -- is refused by name, because the upstreams here are sent text and a
/// part silently dropped would be an answer to a question nobody asked.
/// </summary>
public sealed class MessageContentJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString() ?? "";

            // An assistant turn that only called tools has null content.
            case JsonTokenType.Null:
                return "";

            case JsonTokenType.StartArray:
                return ReadParts(ref reader);

            default:
                throw new JsonException("Message content must be a string or an array of content parts.");
        }
    }

    private static string ReadParts(ref Utf8JsonReader reader)
    {
        var text = new StringBuilder();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            using var part = JsonDocument.ParseValue(ref reader);
            var root = part.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                Append(text, root.GetString());
                continue;
            }

            var type = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("type", out var t)
                ? t.GetString()
                : null;

            if (type != "text")
            {
                throw new JsonException(
                    $"Content part type '{type ?? "unknown"}' is not supported: this gateway forwards text only.");
            }

            Append(text, root.TryGetProperty("text", out var value) ? value.GetString() : null);
        }

        return text.ToString();
    }

    private static void Append(StringBuilder text, string? part)
    {
        if (string.IsNullOrEmpty(part)) return;
        if (text.Length > 0) text.Append('\n');
        text.Append(part);
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
