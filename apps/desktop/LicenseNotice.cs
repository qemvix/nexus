using System.Windows;

namespace Nexus;

/// <summary>The installer and settings hub show the same license embedded at build time.</summary>
public static class LicenseNotice
{
    public static string Text
    {
        get
        {
            using var stream = typeof(LicenseNotice).Assembly.GetManifestResourceStream("Nexus.LICENSE") ?? throw new InvalidDataException("The Nexus license is missing from this build.");
            using var reader = new StreamReader(stream); return reader.ReadToEnd();
        }
    }
    public static void Show() => MessageBox.Show(Text, "Nexus license", MessageBoxButton.OK, MessageBoxImage.Information);
}
