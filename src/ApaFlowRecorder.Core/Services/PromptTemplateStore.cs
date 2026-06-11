namespace ApaFlowRecorder.Core.Services;

public sealed class PromptTemplateStore
{
    private readonly string _templatePath;

    public PromptTemplateStore(string? templatePath = null)
    {
        _templatePath = string.IsNullOrWhiteSpace(templatePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ApaFlowRecorder",
                "prompt-template.md")
            : templatePath;
    }

    public string TemplatePath => _templatePath;

    public string LoadOrCreateDefault()
    {
        if (!File.Exists(_templatePath))
        {
            SaveDefault();
        }

        try
        {
            var template = File.ReadAllText(_templatePath);
            return string.IsNullOrWhiteSpace(template)
                ? GeneralizedRequirementPromptBuilder.DefaultTemplate
                : template;
        }
        catch
        {
            return GeneralizedRequirementPromptBuilder.DefaultTemplate;
        }
    }

    public void Save(string template)
    {
        var directory = Path.GetDirectoryName(_templatePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_templatePath, template);
    }

    public string ResetToDefault()
    {
        SaveDefault();
        return GeneralizedRequirementPromptBuilder.DefaultTemplate;
    }

    private void SaveDefault() => Save(GeneralizedRequirementPromptBuilder.DefaultTemplate);
}
