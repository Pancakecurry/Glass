using System.Runtime.InteropServices;
using System.Text;
using Glass.Core.Product;

namespace Glass.App.Runtime;

internal static partial class StartupFailureReporter
{
    private const uint MessageBoxOk = 0x00000000;
    private const uint MessageBoxIconError = 0x00000010;

    public static string? TryWrite(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.GetTempPath(),
        };

        foreach (var root in roots.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct())
        {
            try
            {
                var logDirectory = Path.Combine(root, "Glass", "logs");
                Directory.CreateDirectory(logDirectory);
                var logPath = Path.Combine(logDirectory, "startup-error.log");
                File.WriteAllText(logPath, Format(exception), Encoding.UTF8);
                return logPath;
            }
            catch (Exception reportingException)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Glass could not write its startup diagnostic: {reportingException}");
            }
        }

        return null;
    }

    public static void TryShow(string? logPath)
    {
        if (!OperatingSystem.IsWindows()) return;

        var message = logPath is null
            ? "Glass couldn't start. A diagnostic log could not be written."
            : $"Glass couldn't start. A diagnostic log was written to:\n\n{logPath}";
        try
        {
            _ = MessageBoxW(0, message, "Glass", MessageBoxOk | MessageBoxIconError);
        }
        catch (Exception reportingException)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Glass could not show its startup error dialog: {reportingException}");
        }
    }

    private static string Format(Exception exception)
    {
        var builder = new StringBuilder()
            .Append("UTC timestamp: ").AppendLine(DateTimeOffset.UtcNow.ToString("O"))
            .Append("Glass version: ").AppendLine(ProductVersion.Current.Informational)
            .Append("OS version: ").AppendLine(RuntimeInformation.OSDescription);

        Exception? current = exception;
        for (var depth = 0; current is not null && depth < 16; depth++, current = current.InnerException)
        {
            builder.AppendLine()
                .Append("Exception ").Append(depth + 1).Append(" type: ")
                .AppendLine(current.GetType().FullName)
                .Append("Message: ").AppendLine(current.Message)
                .AppendLine("Stack trace:")
                .AppendLine(current.StackTrace ?? "<not available>");
        }

        return builder.ToString();
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(nint window, string text, string caption, uint type);
}
