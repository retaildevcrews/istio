// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Ngsa.Middleware;

namespace Ngsa.Application
{
    /// <summary>
    /// Main application class
    /// </summary>
    public sealed partial class App
    {
        // capture parse errors from env vars
        private static readonly List<string> EnvVarErrors = new ();

        /// <summary>
        /// Run the app
        /// </summary>
        /// <param name="config">command line config</param>
        /// <returns>status</returns>
        public static async Task<int> RunApp(Config config)
        {
            NgsaLog logger = new () { Name = typeof(App).FullName };

            // start collecting CPU usage
            CpuCounter.Start();

            try
            {
                SetConfig(config);
                Console.WriteLine($"Istio Trace Val Found: {config.UseIstioTraceID}");


                // build the host
                IWebHost host = BuildHost();

                if (host == null)
                {
                    return -1;
                }

                // display dry run message
                if (config.DryRun)
                {
                    return DoDryRun();
                }

                // setup sigterm handler
                CancellationTokenSource ctCancel = SetupSigTermHandler(host, logger);

                // log startup messages
                LogStartup(logger);

                // start burst metrics service
                if (config.BurstHeader)
                {
                    BurstMetricsService.Init(ctCancel.Token);
                    BurstMetricsService.Start();
                }

                // start the webserver
                Task w = host.RunAsync();

                // this doesn't return except on ctl-c or sigterm
                await w.ConfigureAwait(false);

                // if not cancelled, app exit -1
                return ctCancel.IsCancellationRequested ? 0 : -1;
            }
            catch (Exception ex)
            {
                // end app on error
                logger.LogError(nameof(RunApp), "Exception", ex: ex);

                return -1;
            }
        }

        /// <summary>
        /// Build the RootCommand for parsing
        /// </summary>
        /// <returns>RootCommand</returns>
        public static RootCommand BuildRootCommand()
        {
            RootCommand root = new ()
            {
                Name = "Ngsa.Application",
                Description = "NGSA Validation App",
                TreatUnmatchedTokensAsErrors = true,
            };

            // add the options
            root.AddOption(EnvVarOption(new string[] { "--app-type", "-a" }, "Application Type", AppType.App));
            root.AddOption(EnvVarOption(new string[] { "--prometheus", "-p" }, "Send metrics to Prometheus", false));
            root.AddOption(EnvVarOption(new string[] { "--in-memory", "-m" }, "Use in-memory database", false));
            root.AddOption(EnvVarOption(new string[] { "--no-cache", "-n" }, "Don't cache results", false));
            root.AddOption(EnvVarOption(new string[] { "--url-prefix" }, "URL prefix for ingress mapping", string.Empty));
            root.AddOption(EnvVarOption(new string[] { "--port" }, "Listen Port", 8080, 1, (64 * 1024) - 1));
            root.AddOption(EnvVarOption(new string[] { "--cache-duration", "-d" }, "Cache for duration (seconds)", 300, 1));
            root.AddOption(EnvVarOption(new string[] { "--burst-header" }, "Enable burst metrics header in health and version endpoints. If true, the other burst-service* args/env must be set.", false));
            root.AddOption(EnvVarOption(new string[] { "--burst-service-endpoint" }, "Burst metrics service endpoint", string.Empty));
            root.AddOption(EnvVarOption(new string[] { "--burst-service-ns" }, "Namespace parameter for burst metrics service", string.Empty));
            root.AddOption(EnvVarOption(new string[] { "--burst-service-hpa" }, "HPA name parameter for burst metrics service", string.Empty));
            root.AddOption(EnvVarOption(new string[] { "--retries" }, "Cosmos 429 retries", 10, 0));
            root.AddOption(EnvVarOption(new string[] { "--timeout" }, "Request timeout", 10, 1));
            root.AddOption(EnvVarOption(new string[] { "--data-service", "-s" }, "Data Service URL", string.Empty));
            root.AddOption(EnvVarOption(new string[] { "--secrets-volume", "-v" }, "Secrets Volume Path", "secrets"));
            root.AddOption(EnvVarOption(new string[] { "--zone", "-z" }, "Zone for log", "dev"));
            root.AddOption(EnvVarOption(new string[] { "--region", "-r" }, "Region for log", "dev"));
            root.AddOption(EnvVarOption(new string[] { "--log-level", "-l" }, "Log Level", LogLevel.Error));
            root.AddOption(EnvVarOption(new string[] { "--request-log-level", "-q" }, "Request Log Level", LogLevel.Information));
            root.AddOption(new Option<bool>(new string[] { "--dry-run" }, "Validates configuration"));
            root.AddOption(EnvVarOption(new string[] { "--use-istio-trace-id" }, "Enable Istio Proxy provided trace and request ID instead of Correlation Vector.", false));
            root.AddOption(EnvVarOption(new string[] { "--istio-trace-header-name" }, "Istio Header name for Trace ID.", "x-b3-traceid"));
            root.AddOption(EnvVarOption(new string[] { "--istio-req-header-name" }, "Istio Header name for Request ID.", "x-request-id"));
            root.AddOption(new Option<List<string>>(new string[] { "--propagate-apis" }, ParseStringList, false, "API Server(s) to call"));

            // validate dependencies
            root.AddValidator(ValidateDependencies);

            return root;
        }

        /// <summary>
        /// Parses the string list command line arg (--files).
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>The Argument Value.</returns>
        public static List<string> ParseStringList(ArgumentResult result)
        {
            string name = result.Parent?.Symbol.Name.ToUpperInvariant().Replace('-', '_');
            if (string.IsNullOrWhiteSpace(name))
            {
                result.ErrorMessage = "result.Parent is null";
                return null;
            }

            List<string> val = new ();

            if (result.Tokens.Count == 0)
            {
                string env = Environment.GetEnvironmentVariable(name);

                if (string.IsNullOrWhiteSpace(env))
                {
                    result.ErrorMessage = $"--{result.Argument.Name} is a required parameter";
                    return null;
                }

                string[] files = env.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (string f in files)
                {
                    val.Add(f.Trim());
                }
            }
            else
            {
                for (int i = 0; i < result.Tokens.Count; i++)
                {
                    val.Add(result.Tokens[i].Value.Trim());
                }
            }

            return val;
        }

        // validate combinations of parameters
        private static string ValidateDependencies(CommandResult result)
        {
            string msg = string.Empty;

            if (EnvVarErrors.Count > 0)
            {
                msg += string.Join('\n', EnvVarErrors) + '\n';
            }

            try
            {
                // get the values to validate
                AppType appType = result.Children.FirstOrDefault(c => c.Symbol.Name == "app-type") is OptionResult appTypeRes ? appTypeRes.GetValueOrDefault<AppType>() : AppType.App;
                string secrets = result.Children.FirstOrDefault(c => c.Symbol.Name == "secrets-volume") is OptionResult secretsRes ? secretsRes.GetValueOrDefault<string>() : string.Empty;
                string dataService = result.Children.FirstOrDefault(c => c.Symbol.Name == "data-service") is OptionResult dsRes ? dsRes.GetValueOrDefault<string>() : string.Empty;
                string urlPrefix = result.Children.FirstOrDefault(c => c.Symbol.Name == "urlPrefix") is OptionResult urlRes ? urlRes.GetValueOrDefault<string>() : string.Empty;
                string bsEndpoint = result.Children.FirstOrDefault(c => c.Symbol.Name == "burst-service-endpoint") is OptionResult bsEndpointRes ? bsEndpointRes.GetValueOrDefault<string>() : string.Empty;
                string bsNamespace = result.Children.FirstOrDefault(c => c.Symbol.Name == "burst-service-ns") is OptionResult bsNamespaceRes ? bsNamespaceRes.GetValueOrDefault<string>() : string.Empty;
                string bsHpa = result.Children.FirstOrDefault(c => c.Symbol.Name == "burst-service-hpa") is OptionResult bsHpaRes ? bsHpaRes.GetValueOrDefault<string>() : string.Empty;
                bool inMemory = result.Children.FirstOrDefault(c => c.Symbol.Name == "in-memory") is OptionResult inMemoryRes && inMemoryRes.GetValueOrDefault<bool>();
                bool noCache = result.Children.FirstOrDefault(c => c.Symbol.Name == "no-cache") is OptionResult noCacheRes && noCacheRes.GetValueOrDefault<bool>();
                bool burstHeader = result.Children.FirstOrDefault(c => c.Symbol.Name == "burst-header") is OptionResult burstHeaderRes && burstHeaderRes.GetValueOrDefault<bool>();

                // validate url-prefix
                if (!string.IsNullOrWhiteSpace(urlPrefix))
                {
                    urlPrefix = urlPrefix.Trim();

                    if (urlPrefix.Length < 2)
                    {
                        msg += "--url-prefix is invalid";
                    }

                    if (!urlPrefix.StartsWith('/'))
                    {
                        msg += "--url-prefix must start with /";
                    }
                }

                // validate data-service
                if (appType == AppType.WebAPI)
                {
                    if (string.IsNullOrWhiteSpace(dataService))
                    {
                        msg += "--data-service cannot be empty\n";
                    }
                    else
                    {
                        string ds = dataService.ToLowerInvariant().Trim();

                        if (!ds.StartsWith("http://") &&
                            !ds.StartsWith("https://") &&
                            !ds.Contains(' ') &&
                            !ds.Contains('\t') &&
                            !ds.Contains('\n') &&
                            !ds.Contains('\r'))
                        {
                            msg += "--data-service is invalid";
                        }

                        ds = ds.Replace("http://", string.Empty).Replace("https://", string.Empty);

                        if (string.IsNullOrEmpty(ds))
                        {
                            msg += "--data-service is invalid";
                        }
                    }
                }

                // validate secrets volume
                if (!inMemory && appType == AppType.App)
                {
                    if (string.IsNullOrWhiteSpace(secrets))
                    {
                        msg += "--secrets-volume cannot be empty\n";
                    }
                    else
                    {
                        try
                        {
                            // validate secrets-volume exists
                            if (!Directory.Exists(secrets))
                            {
                                msg += $"--secrets-volume ({secrets}) does not exist\n";
                            }
                        }
                        catch (Exception ex)
                        {
                            msg += $"--secrets-volume exception: {ex.Message}\n";
                        }
                    }
                }

                // validate burst headers
                if (burstHeader)
                {
                    if (string.IsNullOrWhiteSpace(bsEndpoint) ||
                        string.IsNullOrWhiteSpace(bsNamespace) ||
                        string.IsNullOrWhiteSpace(bsHpa))
                    {
                        msg += "burst metrics service variable(s) cannot be empty\n";
                    }
                    else if (!Uri.IsWellFormedUriString($"{bsEndpoint}/{bsNamespace}/{bsHpa}", UriKind.Absolute))
                    {
                        msg += "burst metrics service endpoint is not a valid URI\n";
                    }
                }

                // invalid combination
                if (inMemory && noCache)
                {
                    msg += "--in-memory and --no-cache are exclusive\n";
                }
            }
            catch
            {
                // system.commandline will catch and display parse exceptions
            }

            // return error message(s) or string.empty
            return msg;
        }

        // insert env vars as default
        private static Option EnvVarOption<T>(string[] names, string description, T defaultValue)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentNullException(nameof(description));
            }

            // this will throw on bad names
            string env = GetValueFromEnvironment(names, out string key);

            T value = defaultValue;

            // set default to environment value if set
            if (!string.IsNullOrWhiteSpace(env))
            {
                if (defaultValue.GetType().IsEnum)
                {
                    if (Enum.TryParse(defaultValue.GetType(), env, true, out object result))
                    {
                        value = (T)result;
                    }
                    else
                    {
                        EnvVarErrors.Add($"Environment variable {key} is invalid");
                    }
                }
                else
                {
                    try
                    {
                        value = (T)Convert.ChangeType(env, typeof(T));
                    }
                    catch
                    {
                        EnvVarErrors.Add($"Environment variable {key} is invalid");
                    }
                }
            }

            return new Option<T>(names, () => value, description);
        }

        // insert env vars as default with min val for ints
        private static Option<int> EnvVarOption(string[] names, string description, int defaultValue, int minValue, int? maxValue = null)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentNullException(nameof(description));
            }

            // this will throw on bad names
            string env = GetValueFromEnvironment(names, out string key);

            int value = defaultValue;

            // set default to environment value if set
            if (!string.IsNullOrWhiteSpace(env))
            {
                if (!int.TryParse(env, out value))
                {
                    EnvVarErrors.Add($"Environment variable {key} is invalid");
                }
            }

            Option<int> opt = new (names, () => value, description);

            opt.AddValidator((res) =>
            {
                string s = string.Empty;
                int val;

                try
                {
                    val = (int)res.GetValueOrDefault();

                    if (val < minValue)
                    {
                        s = $"{names[0]} must be >= {minValue}";
                    }
                }
                catch
                {
                }

                return s;
            });

            if (maxValue != null)
            {
                opt.AddValidator((res) =>
                {
                    string s = string.Empty;
                    int val;

                    try
                    {
                        val = (int)res.GetValueOrDefault();

                        if (val > maxValue)
                        {
                            s = $"{names[0]} must be <= {maxValue}";
                        }
                    }
                    catch
                    {
                    }

                    return s;
                });
            }

            return opt;
        }

        // check for environment variable value
        private static string GetValueFromEnvironment(string[] names, out string key)
        {
            if (names == null ||
                names.Length < 1 ||
                names[0].Trim().Length < 4)
            {
                throw new ArgumentNullException(nameof(names));
            }

            for (int i = 1; i < names.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(names[i]) ||
                    names[i].Length != 2 ||
                    names[i][0] != '-')
                {
                    throw new ArgumentException($"Invalid command line parameter at position {i}", nameof(names));
                }
            }

            key = names[0][2..].Trim().ToUpperInvariant().Replace('-', '_');

            return Environment.GetEnvironmentVariable(key);
        }

        // set config values from command line
        private static void SetConfig(Config config)
        {
            // copy command line values
            Config.SetConfig(config);

            // create data access layer
            if (Config.AppType == AppType.App)
            {
                LoadSecrets();

                // load the cache
                Config.CacheDal = new DataAccessLayer.InMemoryDal();

                // create the cosomos data access layer
                if (Config.InMemory)
                {
                    Config.CosmosDal = Config.CacheDal;
                }
                else
                {
                    Config.CosmosDal = new DataAccessLayer.CosmosDal(Config.Secrets, Config);
                }
            }

            SetLoggerConfig();
        }

        // set the logger config
        private static void SetLoggerConfig()
        {
            RequestLogger.CosmosName = Config.CosmosName;
            RequestLogger.DataService = Config.DataService.Replace("http://", string.Empty).Replace("https://", string.Empty);
            RequestLogger.Zone = Config.Zone;
            RequestLogger.Region = Config.Region;

            NgsaLogger.Zone = Config.Zone;
            NgsaLogger.Region = Config.Region;

            NgsaLog.Zone = Config.Zone;
            NgsaLog.Region = Config.Region;
            NgsaLog.LogLevel = Config.LogLevel;
        }

        // Display the dry run message
        private static int DoDryRun()
        {
            Console.WriteLine($"Version            {VersionExtension.Version}");
            Console.WriteLine($"Application Type   {Config.AppType}");
            Console.WriteLine($"Use Prometheus     {Config.Prometheus}");

            if (Config.AppType == AppType.WebAPI)
            {
                Console.WriteLine($"Data Service       {Config.DataService}");
                Console.WriteLine($"Request Timeout    {Config.Timeout}");
            }
            else
            {
                Console.WriteLine($"In Memory          {Config.InMemory}");
                Console.WriteLine($"No Cache           {Config.NoCache}");

                if (!Config.InMemory)
                {
                    Console.WriteLine($"Cosmos Server      {Config.Secrets.CosmosServer}");
                    Console.WriteLine($"Cosmos Database    {Config.Secrets.CosmosDatabase}");
                    Console.WriteLine($"Cosmos Collection  {Config.Secrets.CosmosCollection}");
                    Console.WriteLine($"Cosmos Key         Length({Config.Secrets.CosmosKey.Length})");
                    Console.WriteLine($"Cosmos Retries     {Config.Retries}");
                    Console.WriteLine($"Request Timeout    {Config.Timeout}");
                    Console.WriteLine($"Secrets Volume     {Config.Secrets.Volume}");
                }
            }

            Console.WriteLine($"Region             {Config.Region}");
            Console.WriteLine($"Zone               {Config.Zone}");

            Console.WriteLine($"Log Level          {Config.LogLevel}");
            Console.WriteLine($"Request Log Level  {Config.RequestLogLevel}");

            // always return 0 (success)
            return 0;
        }
    }
}
