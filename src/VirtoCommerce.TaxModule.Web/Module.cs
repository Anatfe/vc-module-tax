using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.JsonConverters;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Data.Extensions;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.TaxModule.Core.Services;
using VirtoCommerce.TaxModule.Data.ExportImport;
using VirtoCommerce.TaxModule.Data.Provider;
using VirtoCommerce.TaxModule.Data.Repositories;
using VirtoCommerce.TaxModule.Data.Services;

namespace VirtoCommerce.TaxModule.Web
{
    public class Module : IModule, IExportSupport, IImportSupport
    {
        public ManifestModuleInfo ModuleInfo { get; set; }

        private IApplicationBuilder _appBuilder;
        public void Initialize(IServiceCollection serviceCollection)
        {
            serviceCollection.AddDbContext<TaxDbContext>((provider, options) =>
            {
                var configuration = provider.GetRequiredService<IConfiguration>();
                options.UseSqlServer(configuration.GetConnectionString(ModuleInfo.Id) ?? configuration.GetConnectionString("VirtoCommerce"));
            });

            serviceCollection.AddTransient<ITaxRepository, TaxRepository>();
            serviceCollection.AddTransient<Func<ITaxRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetService<ITaxRepository>());

            serviceCollection.AddTransient<ITaxProviderService, TaxProviderService>();
            serviceCollection.AddTransient<ITaxProviderRegistrar, TaxProviderService>();
            serviceCollection.AddTransient<ITaxProviderSearchService, TaxProviderSearchService>();
            serviceCollection.AddTransient<TaxExportImport>();
        }

        public void PostInitialize(IApplicationBuilder applicationBuilder)
        {
            _appBuilder = applicationBuilder;

            var settingsRegistrar = applicationBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(Core.ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

            PolymorphJsonConverter.RegisterTypeForDiscriminator(typeof(TaxProvider), nameof(TaxProvider.TypeName));

            var taxProviderRegistrar = applicationBuilder.ApplicationServices.GetRequiredService<ITaxProviderRegistrar>();
            taxProviderRegistrar.RegisterTaxProvider<FixedRateTaxProvider>();
            settingsRegistrar.RegisterSettingsForType(Core.ModuleConstants.Settings.FixedTaxProviderSettings.AllSettings, typeof(FixedRateTaxProvider).Name);

            using (var serviceScope = applicationBuilder.ApplicationServices.CreateScope())
            {
                var dbContext = serviceScope.ServiceProvider.GetRequiredService<TaxDbContext>();
                dbContext.Database.MigrateIfNotApplied(MigrationName.GetUpdateV2MigrationName(ModuleInfo.Id));
                dbContext.Database.EnsureCreated();
                dbContext.Database.Migrate();
            }
        }

        public void Uninstall()
        {
        }

        public async Task ExportAsync(Stream outStream, ExportImportOptions options, Action<ExportImportProgressInfo> progressCallback,
            ICancellationToken cancellationToken)
        {
            await _appBuilder.ApplicationServices.GetRequiredService<TaxExportImport>().DoExportAsync(outStream, progressCallback, cancellationToken);
        }

        public async Task ImportAsync(Stream inputStream, ExportImportOptions options, Action<ExportImportProgressInfo> progressCallback,
            ICancellationToken cancellationToken)
        {
            await _appBuilder.ApplicationServices.GetRequiredService<TaxExportImport>().DoImportAsync(inputStream, progressCallback, cancellationToken);
        }
    }
}
