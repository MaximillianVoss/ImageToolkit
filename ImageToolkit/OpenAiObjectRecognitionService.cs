using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ImageToolkit.Core;

namespace ImageToolkit;

public sealed class OpenAiObjectRecognitionService
{
    private const string DefaultModel = "gpt-4.1-mini";
    private static readonly Uri ChatCompletionsUri = new("https://api.openai.com/v1/chat/completions");
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public OpenAiObjectRecognitionService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };
    }

    public async Task<ImageRecognitionResult> RecognizeAsync(
        byte[] imageBytes,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        if (imageBytes.Length == 0)
        {
            throw new ArgumentException("Image payload is empty.", nameof(imageBytes));
        }

        if (string.IsNullOrWhiteSpace(mediaType))
        {
            throw new ArgumentException("Media type is required.", nameof(mediaType));
        }

        var apiKey = OpenAiApiKeyResolver.Resolve();

        var model = Environment.GetEnvironmentVariable("OPENAI_VISION_MODEL");
        if (string.IsNullOrWhiteSpace(model))
        {
            model = DefaultModel;
        }

        var payload = BuildRequestPayload(model, imageBytes, mediaType);
        using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, SerializerOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildApiErrorMessage(response.StatusCode, responseText));
        }

        var jsonText = ExtractCompletionJson(responseText);
        var recognizedPayload = JsonSerializer.Deserialize<RecognitionPayload>(jsonText, SerializerOptions)
            ?? throw new InvalidOperationException("Не удалось разобрать JSON с результатом распознавания.");

        var result = new ImageRecognitionResult(
            recognizedPayload.SceneSummary ?? string.Empty,
            (recognizedPayload.Objects ?? [])
                .Select(static item => new RecognizedObject(
                    item.Name ?? string.Empty,
                    item.Count,
                    item.Description ?? string.Empty))
                .ToArray(),
            recognizedPayload.ExtractedText ?? string.Empty);

        return result.Normalize();
    }

    private static object BuildRequestPayload(string model, byte[] imageBytes, string mediaType)
    {
        var imageDataUrl = $"data:{mediaType};base64,{Convert.ToBase64String(imageBytes)}";

        return new
        {
            model,
            temperature = 0,
            max_tokens = 600,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content =
                        "Ты анализируешь изображение и возвращаешь только структурированный JSON. " +
                        "Определи основные видимые объекты. Для каждого объекта используй короткое имя в единственном числе и нижнем регистре на русском языке. " +
                        "Если один и тот же объект встречается несколько раз, верни его один раз и укажи count. " +
                        "Если на изображении есть читаемый текст, помести его в extracted_text без выдумывания."
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text =
                                "Распознай объекты на изображении. " +
                                "Верни краткое описание сцены, список объектов и видимый текст. " +
                                "Не добавляй ничего кроме JSON по схеме."
                        },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = imageDataUrl
                            }
                        }
                    }
                }
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "image_object_recognition",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            scene_summary = new
                            {
                                type = "string"
                            },
                            extracted_text = new
                            {
                                type = "string"
                            },
                            objects = new
                            {
                                type = "array",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        name = new
                                        {
                                            type = "string"
                                        },
                                        count = new
                                        {
                                            type = "integer"
                                        },
                                        description = new
                                        {
                                            type = "string"
                                        }
                                    },
                                    required = new[] { "name", "count", "description" },
                                    additionalProperties = false
                                }
                            }
                        },
                        required = new[] { "scene_summary", "extracted_text", "objects" },
                        additionalProperties = false
                    }
                }
            }
        };
    }

    private static string ExtractCompletionJson(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("OpenAI API не вернуло ни одного варианта ответа.");
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message))
        {
            throw new InvalidOperationException("В ответе OpenAI API отсутствует блок message.");
        }

        if (message.TryGetProperty("refusal", out var refusalElement) &&
            refusalElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(refusalElement.GetString()))
        {
            throw new InvalidOperationException($"Модель отказалась выполнять запрос: {refusalElement.GetString()}");
        }

        if (!message.TryGetProperty("content", out var contentElement))
        {
            throw new InvalidOperationException("В ответе OpenAI API отсутствует content.");
        }

        var content = ExtractContentText(contentElement);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("OpenAI API вернуло пустой ответ.");
        }

        return content;
    }

    private static string ExtractContentText(JsonElement contentElement)
    {
        if (contentElement.ValueKind == JsonValueKind.String)
        {
            return contentElement.GetString() ?? string.Empty;
        }

        if (contentElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Получен неожиданный формат content от OpenAI API.");
        }

        var builder = new StringBuilder();
        foreach (var item in contentElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("type", out var typeElement) ||
                typeElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var type = typeElement.GetString();
            if ((type is "text" or "output_text") &&
                item.TryGetProperty("text", out var textElement) &&
                textElement.ValueKind == JsonValueKind.String)
            {
                builder.Append(textElement.GetString());
            }
        }

        return builder.ToString();
    }

    private static string BuildApiErrorMessage(HttpStatusCode statusCode, string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (document.RootElement.TryGetProperty("error", out var errorElement) &&
                errorElement.TryGetProperty("message", out var messageElement) &&
                messageElement.ValueKind == JsonValueKind.String)
            {
                return $"OpenAI API вернуло ошибку {(int)statusCode}: {messageElement.GetString()}";
            }
        }
        catch (JsonException)
        {
        }

        return $"OpenAI API вернуло ошибку {(int)statusCode}.";
    }

    private sealed record RecognitionPayload(string SceneSummary, string ExtractedText, IReadOnlyList<RecognitionObjectPayload> Objects);

    private sealed record RecognitionObjectPayload(string Name, int Count, string Description);
}
