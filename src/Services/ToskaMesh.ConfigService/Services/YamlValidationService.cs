using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ToskaMesh.ConfigService.Services;

public interface IYamlValidationService
{
    bool TryValidate(string yaml, out string? error);
}

public class YamlValidationService : IYamlValidationService
{
    public bool TryValidate(string yaml, out string? error)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            error = null;
            return true;
        }
        catch (YamlException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
