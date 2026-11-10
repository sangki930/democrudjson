using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DemoCrud.Api.Services;

public sealed class LayerPatchException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

// Deliberately supports add/remove/replace/test, restricted to the data subtree.
public static class LayerJsonPatch
{
    public static JsonElement Apply(JsonElement data, JsonElement patch)
    {
        if (patch.ValueKind != JsonValueKind.Array || patch.GetArrayLength() > 100)
            throw new LayerPatchException("Patch must be an array containing at most 100 operations.");
        var root = new JsonObject { ["data"] = JsonNode.Parse(data.GetRawText()) };
        foreach (var operation in patch.EnumerateArray())
        {
            if (operation.ValueKind != JsonValueKind.Object ||
                !operation.TryGetProperty("op", out var opValue) || opValue.ValueKind != JsonValueKind.String ||
                !operation.TryGetProperty("path", out var pathValue) || pathValue.ValueKind != JsonValueKind.String)
                throw new LayerPatchException("Each operation requires string op and path fields.");
            var op = opValue.GetString()!;
            if (op is not ("add" or "remove" or "replace" or "test"))
                throw new LayerPatchException("Supported operations: add, remove, replace, test.");
            var path = pathValue.GetString()!;
            if (path != "/data" && !path.StartsWith("/data/", StringComparison.Ordinal))
                throw new LayerPatchException("Only /data and its descendants can be patched.");
            var tokens = path.Split('/').Skip(1).Select(Decode).ToArray();
            JsonNode? parent = root;
            foreach (var token in tokens[..^1]) parent = Get(parent, token);
            var key = tokens[^1];
            JsonNode? value = null;
            if (op != "remove")
            {
                if (!operation.TryGetProperty("value", out var valueElement))
                    throw new LayerPatchException("This operation requires a value (JSON null is allowed).");
                value = JsonNode.Parse(valueElement.GetRawText());
            }
            if (op == "test")
            {
                if (!JsonNode.DeepEquals(Get(parent, key), value))
                    throw new LayerPatchException("The test operation failed; no changes were saved.", 409);
                continue;
            }
            if (parent is JsonObject obj)
            {
                if (op != "add" && !obj.ContainsKey(key)) throw new LayerPatchException("Target property does not exist.");
                if (op == "remove") obj.Remove(key);
                else obj[key] = value;
            }
            else if (parent is JsonArray array)
            {
                var index = Index(key, array.Count, op == "add");
                if (op == "add") array.Insert(index, value);
                else if (op == "remove") array.RemoveAt(index);
                else array[index] = value;
            }
            else throw new LayerPatchException("Parent path must refer to an object or array.");
            if (root["data"] is not JsonObject)
                throw new LayerPatchException("data must remain a JSON object.");
        }
        return JsonSerializer.SerializeToElement(root["data"]);
    }

    private static JsonNode? Get(JsonNode? node, string token)
    {
        if (node is JsonObject obj && obj.TryGetPropertyValue(token, out var value)) return value;
        if (node is JsonArray array) return array[Index(token, array.Count, false)];
        throw new LayerPatchException("The JSON path does not exist.");
    }

    private static int Index(string token, int count, bool adding)
    {
        if (adding && token == "-") return count;
        if (token.Length == 0 || (token.Length > 1 && token[0] == '0') ||
            !token.All(char.IsAsciiDigit) ||
            !int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
            index >= (long)count + (adding ? 1 : 0))
            throw new LayerPatchException("Invalid array index.");
        return index;
    }

    private static string Decode(string token)
    {
        for (var i = 0; i < token.Length; i++)
            if (token[i] == '~' && (++i == token.Length || token[i] is not ('0' or '1')))
                throw new LayerPatchException("Invalid JSON Pointer escape.");
        return token.Replace("~1", "/").Replace("~0", "~");
    }
}
