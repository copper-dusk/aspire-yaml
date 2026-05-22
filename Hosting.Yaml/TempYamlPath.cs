namespace CopperDusk.Aspire.Hosting.Yaml;

public static class TempYamlPath
{
    public const string WorkingDirectory = ".aspire-yaml";
    
    public static string BuildTempYamlPath(string suffix)
    {
        var tempRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var tempWorkingDirecotry = Path.Join(tempRoot, WorkingDirectory);
        
        Directory.CreateDirectory(tempWorkingDirecotry);

        return Path.Join(tempRoot, WorkingDirectory, suffix);
    }
}