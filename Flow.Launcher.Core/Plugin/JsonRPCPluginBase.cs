using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Plugin;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Control = System.Windows.Controls.Control;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// Represent the plugin that using JsonPRC
    /// every JsonRPC plugin should has its own plugin instance
    /// </summary>
    public abstract class JsonRPCPluginBase : IAsyncPlugin, IContextMenu, ISettingProvider, ISavable
    {
        public const string JsonRPC = "JsonRPC";

        protected PluginInitContext Context;

        private string SettingConfigurationPath =>
            Path.Combine(Context.CurrentPluginMetadata.PluginDirectory, "SettingsTemplate.yaml");

        private string SettingDirectory => Context.CurrentPluginMetadata.PluginSettingsDirectoryPath;

        private string SettingPath => Path.Combine(SettingDirectory, "Settings.json");

        public abstract List<Result> LoadContextMenus(Result selectedResult);

        protected static readonly JsonSerializerOptions DeserializeOption = new()
        {
            PropertyNameCaseInsensitive = true,
#pragma warning disable SYSLIB0020
            // IgnoreNullValues is obsolete, but the replacement JsonIgnoreCondition.WhenWritingNull still 
            // deserializes null, instead of ignoring it and leaving the default (empty list). We can change the behaviour
            // to accept null and fallback to a default etc, or just keep IgnoreNullValues for now
            // see: https://github.com/dotnet/runtime/issues/39152
            IgnoreNullValues = true,
#pragma warning restore SYSLIB0020 // Type or member is obsolete
            Converters = { new JsonObjectConverter() }
        };

        protected static readonly JsonSerializerOptions RequestSerializeOption = new()
        {
            PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        protected abstract Task<bool> ExecuteResultAsync(JsonRPCResult result);

        protected JsonRPCPluginSettings Settings { get; set; }

        protected List<Result> ParseResults(JsonRPCQueryResponseModel queryResponseModel)
        {
            if (queryResponseModel.Result == null) return null;

            if (!string.IsNullOrEmpty(queryResponseModel.DebugMessage))
            {
                Context.API.ShowMsg(queryResponseModel.DebugMessage);
            }

            foreach (var result in queryResponseModel.Result)
            {
                result.AsyncAction = async _ =>
                {
                    Settings?.UpdateSettings(result.SettingsChange);

                    return await ExecuteResultAsync(result);
                };
            }

            var results = new List<Result>();

            results.AddRange(queryResponseModel.Result);

            Settings?.UpdateSettings(queryResponseModel.SettingsChange);

            return results;
        }

        protected void ExecuteFlowLauncherAPI(string method, object[] parameters)
        {
            // RPC callers may omit optional parameters or pass null, and null
            // arguments carry no type information for GetMethod(Type[]). Resolve
            // the overload by name and arity instead: supplied arguments must be
            // assignable, null arguments must sit on a nullable parameter slot,
            // and trailing parameters omitted by the caller must be optional.
            var methodInfo = typeof(IPublicAPI).GetMethods()
                .Where(m => string.Equals(m.Name, method, StringComparison.Ordinal))
                .Where(m => m.GetParameters().Length >= parameters.Length)
                .FirstOrDefault(m =>
                {
                    var methodParams = m.GetParameters();
                    for (var i = 0; i < methodParams.Length; i++)
                    {
                        var parameterType = methodParams[i].ParameterType;
                        if (i >= parameters.Length)
                        {
                            if (!methodParams[i].HasDefaultValue)
                            {
                                return false;
                            }

                            continue;
                        }

                        var argument = parameters[i];
                        if (argument == null)
                        {
                            if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                            {
                                return false;
                            }
                        }
                        else if (!parameterType.IsInstanceOfType(argument))
                        {
                            return false;
                        }
                    }

                    return true;
                });

            if (methodInfo == null)
            {
                return;
            }

            try
            {
                var methodParams = methodInfo.GetParameters();
                var invokeArgs = new object[methodParams.Length];
                for (var i = 0; i < methodParams.Length; i++)
                {
                    // Omitted trailing optional parameters are filled with their
                    // declared default values (Type.Missing for compile-time
                    // null defaults on reference types).
                    invokeArgs[i] = i < parameters.Length ? parameters[i] : methodParams[i].DefaultValue;
                }

                methodInfo.Invoke(Context.API, invokeArgs);
            }
            catch (Exception)
            {
#if (DEBUG)
                throw;
#endif
            }
        }

        public abstract Task<List<Result>> QueryAsync(Query query, CancellationToken token);

        private async Task InitSettingAsync()
        {
            JsonRpcConfigurationModel configuration = null;
            if (File.Exists(SettingConfigurationPath))
            {
                var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
                configuration =
                    deserializer.Deserialize<JsonRpcConfigurationModel>(
                        await File.ReadAllTextAsync(SettingConfigurationPath));
            }

            Settings ??= new JsonRPCPluginSettings
            {
                Configuration = configuration, SettingPath = SettingPath, API = Context.API
            };

            await Settings.InitializeAsync();
        }

        public virtual async Task InitAsync(PluginInitContext context)
        {
            Context = context;
            await InitSettingAsync();
        }

        public void Save()
        {
            Settings?.Save();
        }

        public bool NeedCreateSettingPanel()
        {
            return Settings.NeedCreateSettingPanel();
        }

        public Control CreateSettingPanel()
        {
            return Settings.CreateSettingPanel();
        }
    }
}
