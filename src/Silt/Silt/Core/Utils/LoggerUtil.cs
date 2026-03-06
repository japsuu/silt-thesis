using Serilog;

namespace Silt.Core.Utils;

public static class LoggerUtil
{
    private static readonly HashSet<string> _missingUniforms = [];
    
    
    public static void LogMissingUniformOnce(string shaderName, string uniformName)
    {
        string key = $"{shaderName}:{uniformName}";
        if (!_missingUniforms.Add(key))
            return;

        Log.Warning("Shader '{Shader}' is missing uniform '{Uniform}'. Has it been optimized out?", shaderName, uniformName);
    }
}