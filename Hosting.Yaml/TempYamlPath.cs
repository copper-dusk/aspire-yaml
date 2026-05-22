namespace CopperDusk.Aspire.Hosting.Yaml;

public static class TempYamlPath
{
    public const string WorkingDirectory = ".aspire-yaml";
    
    public static string BuildTempYamlPath(string suffix)
    {
        var tempRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return Path.Join(tempRoot, WorkingDirectory, suffix);
    }
}