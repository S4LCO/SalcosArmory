using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SalcosArmory.Configurator.Services;

internal static class JsoncFileStore
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true
    };

    public static JsonObject ParseObject(string text, string description)
    {
        return ParseNode(text, description) as JsonObject
            ?? throw new InvalidDataException($"{description} does not contain a JSON object.");
    }

    public static JsonNode ParseNode(string text, string description)
    {
        try
        {
            return JsonNode.Parse(text, documentOptions: DocumentOptions)
                ?? throw new InvalidDataException($"{description} is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{description} contains invalid JSONC: {ex.Message}", ex);
        }
    }

    public static string Merge(string originalText, JsonObject originalRoot, JsonObject updatedRoot)
    {
        if (JsonNode.DeepEquals(originalRoot, updatedRoot))
        {
            return originalText;
        }

        var syntaxRoot = new JsoncSyntaxParser(originalText).Parse();
        var replacements = new List<Replacement>();
        CollectReplacements(
            syntaxRoot,
            originalRoot,
            updatedRoot,
            originalText,
            replacements);

        if (replacements.Count == 0)
        {
            return originalText;
        }

        var result = new StringBuilder(originalText);
        foreach (var replacement in replacements.OrderByDescending(x => x.Start))
        {
            result.Remove(replacement.Start, replacement.Length);
            result.Insert(replacement.Start, replacement.Text);
        }

        return result.ToString();
    }

    public static void WriteAtomically(string path, string text)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Could not resolve the directory for '{path}'.");
        Directory.CreateDirectory(directory);

        var temporaryFile = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporaryFile, text, new UTF8Encoding(false));
            File.Move(temporaryFile, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }

    private static void CollectReplacements(
        SyntaxNode syntax,
        JsonNode? original,
        JsonNode? updated,
        string originalText,
        List<Replacement> replacements)
    {
        if (JsonNode.DeepEquals(original, updated))
        {
            return;
        }

        if (syntax.Kind == SyntaxKind.Object
            && original is JsonObject originalObject
            && updated is JsonObject updatedObject
            && HaveSameKeys(syntax.Properties.Keys, updatedObject.Select(x => x.Key)))
        {
            foreach (var property in updatedObject)
            {
                if (!syntax.Properties.TryGetValue(property.Key, out var childSyntax))
                {
                    ReplaceWholeNode();
                    return;
                }

                originalObject.TryGetPropertyValue(property.Key, out var originalValue);
                CollectReplacements(
                    childSyntax,
                    originalValue,
                    property.Value,
                    originalText,
                    replacements);
            }

            return;
        }

        if (syntax.Kind == SyntaxKind.Array
            && original is JsonArray originalArray
            && updated is JsonArray updatedArray
            && syntax.Items.Count == updatedArray.Count
            && originalArray.Count == updatedArray.Count)
        {
            for (var index = 0; index < updatedArray.Count; index++)
            {
                CollectReplacements(
                    syntax.Items[index],
                    originalArray[index],
                    updatedArray[index],
                    originalText,
                    replacements);
            }

            return;
        }

        ReplaceWholeNode();

        void ReplaceWholeNode()
        {
            replacements.Add(new Replacement(
                syntax.Start,
                syntax.End - syntax.Start,
                FormatForLocation(updated, originalText, syntax.Start)));
        }
    }

    private static bool HaveSameKeys(IEnumerable<string> first, IEnumerable<string> second)
    {
        return new HashSet<string>(first, StringComparer.Ordinal).SetEquals(second);
    }

    private static string FormatForLocation(JsonNode? node, string originalText, int start)
    {
        var json = node?.ToJsonString(IndentedOptions) ?? "null";
        if (!json.Contains('\n'))
        {
            return json;
        }

        var newline = originalText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lineStart = originalText.LastIndexOf('\n', Math.Max(0, start - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;

        var indentEnd = lineStart;
        while (indentEnd < start && originalText[indentEnd] is ' ' or '\t')
        {
            indentEnd++;
        }

        var indent = originalText[lineStart..indentEnd];
        return json
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", newline + indent, StringComparison.Ordinal);
    }

    private readonly record struct Replacement(int Start, int Length, string Text);

    private enum SyntaxKind
    {
        Object,
        Array,
        Value
    }

    private sealed class SyntaxNode
    {
        public required SyntaxKind Kind { get; init; }
        public required int Start { get; init; }
        public int End { get; set; }
        public Dictionary<string, SyntaxNode> Properties { get; } = new(StringComparer.Ordinal);
        public List<SyntaxNode> Items { get; } = [];
    }

    private sealed class JsoncSyntaxParser(string text)
    {
        private int _position;

        public SyntaxNode Parse()
        {
            SkipTrivia();
            var root = ParseValue();
            SkipTrivia();

            if (_position != text.Length)
            {
                throw new InvalidDataException("Unexpected content after the JSONC document.");
            }

            return root;
        }

        private SyntaxNode ParseValue()
        {
            SkipTrivia();
            if (_position >= text.Length)
            {
                throw new InvalidDataException("Unexpected end of the JSONC document.");
            }

            return text[_position] switch
            {
                '{' => ParseObject(),
                '[' => ParseArray(),
                '"' => ParseString(),
                _ => ParsePrimitive()
            };
        }

        private SyntaxNode ParseObject()
        {
            var node = new SyntaxNode
            {
                Kind = SyntaxKind.Object,
                Start = _position,
                End = 0
            };
            _position++;
            SkipTrivia();

            while (_position < text.Length && text[_position] != '}')
            {
                if (text[_position] != '"')
                {
                    throw new InvalidDataException($"Expected a property name at character {_position}.");
                }

                var propertyStart = _position;
                var propertyEnd = ReadStringEnd();
                var propertyName = JsonSerializer.Deserialize<string>(text[propertyStart..propertyEnd])
                    ?? throw new InvalidDataException("A JSON property name cannot be null.");

                SkipTrivia();
                Expect(':');
                var value = ParseValue();
                node.Properties[propertyName] = value;

                SkipTrivia();
                if (_position < text.Length && text[_position] == ',')
                {
                    _position++;
                    SkipTrivia();
                }
                else
                {
                    break;
                }
            }

            Expect('}');
            node.End = _position;
            return node;
        }

        private SyntaxNode ParseArray()
        {
            var node = new SyntaxNode
            {
                Kind = SyntaxKind.Array,
                Start = _position,
                End = 0
            };
            _position++;
            SkipTrivia();

            while (_position < text.Length && text[_position] != ']')
            {
                node.Items.Add(ParseValue());
                SkipTrivia();

                if (_position < text.Length && text[_position] == ',')
                {
                    _position++;
                    SkipTrivia();
                }
                else
                {
                    break;
                }
            }

            Expect(']');
            node.End = _position;
            return node;
        }

        private SyntaxNode ParsePrimitive()
        {
            var start = _position;
            var end = ReadPrimitiveEnd();
            return new SyntaxNode
            {
                Kind = SyntaxKind.Value,
                Start = start,
                End = end
            };
        }

        private SyntaxNode ParseString()
        {
            var start = _position;
            var end = ReadStringEnd();
            return new SyntaxNode
            {
                Kind = SyntaxKind.Value,
                Start = start,
                End = end
            };
        }

        private int ReadStringEnd()
        {
            var escaped = false;
            _position++;

            while (_position < text.Length)
            {
                var character = text[_position++];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == '"')
                {
                    return _position;
                }
            }

            throw new InvalidDataException("Unterminated JSON string.");
        }

        private int ReadPrimitiveEnd()
        {
            var start = _position;
            while (_position < text.Length)
            {
                var character = text[_position];
                if (char.IsWhiteSpace(character) || character is ',' or ']' or '}' or '/')
                {
                    break;
                }

                _position++;
            }

            if (_position == start)
            {
                throw new InvalidDataException($"Expected a JSON value at character {_position}.");
            }

            return _position;
        }

        private void SkipTrivia()
        {
            while (_position < text.Length)
            {
                if (char.IsWhiteSpace(text[_position]))
                {
                    _position++;
                    continue;
                }

                if (_position + 1 >= text.Length || text[_position] != '/')
                {
                    return;
                }

                if (text[_position + 1] == '/')
                {
                    _position += 2;
                    while (_position < text.Length && text[_position] is not '\r' and not '\n')
                    {
                        _position++;
                    }

                    continue;
                }

                if (text[_position + 1] == '*')
                {
                    _position += 2;
                    while (_position + 1 < text.Length
                           && !(text[_position] == '*' && text[_position + 1] == '/'))
                    {
                        _position++;
                    }

                    if (_position + 1 >= text.Length)
                    {
                        throw new InvalidDataException("Unterminated JSONC block comment.");
                    }

                    _position += 2;
                    continue;
                }

                return;
            }
        }

        private void Expect(char expected)
        {
            SkipTrivia();
            if (_position >= text.Length || text[_position] != expected)
            {
                throw new InvalidDataException($"Expected '{expected}' at character {_position}.");
            }

            _position++;
        }
    }
}
