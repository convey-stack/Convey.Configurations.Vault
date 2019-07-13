using System;
using Convey.Configurations.Vault.Stores;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace Convey.Configurations.Vault
{
    public static class Extensions
    {
        private const string SectionName = "vault";
        private const string RegistryName = "configurations.vault";

        public static IConveyBuilder AddVault(this IConveyBuilder builder, string sectionName = SectionName)
        {
            var options = builder.GetOptions<VaultOptions>(sectionName);
            builder.Services.AddSingleton(options);
            if (!options.Enabled || !builder.TryRegister(RegistryName))
            {
                return builder;
            }

            builder.Services.AddTransient<IVaultStore, VaultStore>();

            return builder;
        }

        public static IApplicationBuilder UseVault(this IApplicationBuilder app, string key = null)
        {
            VaultOptions options;
            IConfigurationBuilder configurationBuilder;
            using (var scope = app.ApplicationServices.CreateScope())
            {
                configurationBuilder = scope.ServiceProvider.GetService<IConfigurationBuilder>();
                options = scope.ServiceProvider.GetService<VaultOptions>();
            }

            var enabled = options.Enabled;
            var vaultEnabled = Environment.GetEnvironmentVariable("VAULT_ENABLED")?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(vaultEnabled))
            {
                enabled = vaultEnabled == "true" || vaultEnabled == "1";
            }

            if (enabled)
            {
                configurationBuilder.AddVault(options, key);
            }

            return app;
        }

        private static void AddVault(this IConfigurationBuilder builder,
            VaultOptions options, string key)
        {
            var client = new VaultStore(options);
            var secret = string.IsNullOrWhiteSpace(key)
                ? client.GetDefaultAsync().GetAwaiter().GetResult()
                : client.GetAsync(key).GetAwaiter().GetResult();
            var parser = new JsonParser();
            var data = parser.Parse(JObject.FromObject(secret));
            var source = new MemoryConfigurationSource {InitialData = data};
            builder.Add(source);
        }
    }
}