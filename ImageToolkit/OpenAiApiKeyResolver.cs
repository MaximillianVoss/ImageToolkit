using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ImageToolkit;

internal static class OpenAiApiKeyResolver
{
    private const string ApplicationDirectoryName = "ImageToolkit";
    private const string SecretFileName = "openai.secret.json";
    private const string PortableSecretFileName = "openai.portable.secret.json";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static string Resolve()
    {
        var environmentApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(environmentApiKey))
        {
            return environmentApiKey.Trim();
        }

        var errors = new List<string>();

        foreach (var path in GetCandidatePaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var fileContent = File.ReadAllText(path, Encoding.UTF8);
                var secretFile = JsonSerializer.Deserialize<OpenAiSecretFile>(fileContent, SerializerOptions)
                    ?? throw new InvalidOperationException("Файл секрета пуст или поврежден.");

                if (!string.Equals(secretFile.Provider, "dpapi-current-user", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Неподдерживаемый формат файла секрета.");
                }

                var protectedBytes = Convert.FromBase64String(secretFile.EncryptedApiKey ?? string.Empty);
                var apiKeyBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                var apiKey = Encoding.UTF8.GetString(apiKeyBytes).Trim();

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException("Расшифрованный API-ключ пуст.");
                }

                return apiKey;
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException or JsonException or InvalidOperationException)
            {
                errors.Add($"Локальный секрет {path}: {exception.Message}");
            }
        }

        foreach (var path in GetPortableCandidatePaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var fileContent = File.ReadAllText(path, Encoding.UTF8);
                var secretFile = JsonSerializer.Deserialize<OpenAiSecretFile>(fileContent, SerializerOptions)
                    ?? throw new InvalidOperationException("Файл переносимого секрета пуст или поврежден.");

                if (!string.Equals(secretFile.Provider, "portable-aes-v1", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Неподдерживаемый формат переносимого секрета.");
                }

                var cipherBytes = Convert.FromBase64String(secretFile.EncryptedApiKey ?? string.Empty);
                var apiKeyBytes = DecryptPortableSecret(cipherBytes);
                var apiKey = Encoding.UTF8.GetString(apiKeyBytes).Trim();

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException("Расшифрованный API-ключ пуст.");
                }

                return apiKey;
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException or JsonException or InvalidOperationException)
            {
                errors.Add($"Переносимый секрет {path}: {exception.Message}");
            }
        }

        var baseMessage =
            "Не найден рабочий OpenAI API key. Задайте переменную среды OPENAI_API_KEY, положите рядом с проектом openai.secret.json или openai.portable.secret.json.";

        if (errors.Count == 0)
        {
            throw new InvalidOperationException(baseMessage);
        }

        throw new InvalidOperationException($"{baseMessage} Ошибки проверки: {string.Join(" | ", errors)}");
    }

    public static bool TryResolve(out string? apiKey, out string? errorMessage)
    {
        try
        {
            apiKey = Resolve();
            errorMessage = null;
            return true;
        }
        catch (Exception exception)
        {
            apiKey = null;
            errorMessage = exception.Message;
            return false;
        }
    }

    public static string SaveForCurrentUser(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API-ключ не может быть пустым.", nameof(apiKey));
        }

        var normalizedApiKey = apiKey.Trim();
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(normalizedApiKey),
            null,
            DataProtectionScope.CurrentUser);

        var secretFile = new OpenAiSecretFile(
            "dpapi-current-user",
            Convert.ToBase64String(protectedBytes),
            DateTime.UtcNow.ToString("O"));

        var secretPath = GetDefaultSecretPath();
        Directory.CreateDirectory(Path.GetDirectoryName(secretPath) ?? AppContext.BaseDirectory);
        File.WriteAllText(
            secretPath,
            JsonSerializer.Serialize(secretFile, SerializerOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return secretPath;
    }

    private static IEnumerable<string> GetCandidatePaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var defaultSecretPath = GetDefaultSecretPath();
        if (seen.Add(defaultSecretPath))
        {
            yield return defaultSecretPath;
        }

        foreach (var path in EnumerateBasePaths())
        {
            var fullPath = Path.Combine(path, SecretFileName);
            if (seen.Add(fullPath))
            {
                yield return fullPath;
            }
        }
    }

    private static IEnumerable<string> GetPortableCandidatePaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in EnumerateBasePaths())
        {
            var fullPath = Path.Combine(path, PortableSecretFileName);
            if (seen.Add(fullPath))
            {
                yield return fullPath;
            }
        }
    }

    private static IEnumerable<string> EnumerateBasePaths()
    {
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 6 && current is not null; depth++)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private static byte[] DecryptPortableSecret(byte[] cipherBytes)
    {
        using var aes = Aes.Create();
        aes.Key = Convert.FromHexString(GetPortableKeyHex());
        aes.IV = Convert.FromHexString(GetPortableIvHex());
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
    }

    private static string GetPortableKeyHex()
    {
        return string.Concat(
            "7D3A5D9FE4A119C293D4F7B33D7AA4C1",
            "0F46B19E2C7D889AE6C4310DE3A5C9B7");
    }

    private static string GetPortableIvHex()
    {
        return string.Concat(
            "E18A3C7D5FB29104",
            "4A6ED8C2139F70B5");
    }

    private static string GetDefaultSecretPath()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localApplicationData)
            ? AppContext.BaseDirectory
            : Path.Combine(localApplicationData, ApplicationDirectoryName);

        return Path.Combine(baseDirectory, SecretFileName);
    }

    private sealed record OpenAiSecretFile(string Provider, string EncryptedApiKey, string? CreatedAtUtc);
}
