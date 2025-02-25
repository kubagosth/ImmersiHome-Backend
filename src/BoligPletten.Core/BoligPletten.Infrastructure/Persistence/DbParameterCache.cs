using System.Collections.Concurrent;
using System.Data;

namespace BoligPletten.Infrastructure.Persistence
{
    public static class DbParameterCache
    {
        // Cache parameter creators for frequently used SQL commands
        private static readonly ConcurrentDictionary<string, Action<IDbCommand, object>> _parameterSetters =
            new ConcurrentDictionary<string, Action<IDbCommand, object>>();

        // Cache compiled expressions for setting parameter values
        public static Action<IDbCommand, object> GetParameterSetter(string sqlHash, IEnumerable<string> parameterNames)
        {
            return _parameterSetters.GetOrAdd(sqlHash, _ =>
            {
                // Build a compiled expression to set parameters
                return (cmd, paramObj) =>
                {
                    foreach (var paramName in parameterNames)
                    {
                        var param = cmd.CreateParameter();
                        param.ParameterName = paramName;

                        // Get value from dictionary or object
                        object? value = null;
                        if (paramObj is IDictionary<string, object?> dict)
                        {
                            dict.TryGetValue(paramName, out value);
                        }
                        else
                        {
                            // Property access via reflection (could be optimized with Expression trees)
                            var prop = paramObj.GetType().GetProperty(paramName.TrimStart('@'));
                            if (prop != null)
                            {
                                value = prop.GetValue(paramObj);
                            }
                        }

                        param.Value = value ?? DBNull.Value;
                        cmd.Parameters.Add(param);
                    }
                };
            });
        }
    }
}
