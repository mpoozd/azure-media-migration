﻿using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using System.CommandLine;
using System.CommandLine.Binding;

namespace AMSMigrate
{
    internal class AssetOptionsBinder : BinderBase<AssetOptions>
    {
        private readonly Option<string> _sourceAccount = new Option<string>(
             aliases: new[] { "--source-account-name", "-n" },
             description: "Azure Media Services Account.")
        {
            IsRequired = true,
            Arity = ArgumentArity.ExactlyOne
        };

        private readonly Option<string> _storageAccount = new(
            aliases: new[] { "--output-storage-account", "-o" },
            description: @"The storage account to upload the migrated assets.
This is specific to the cloud you are migrating to.
For Azure specify the storage account name or the URL <https://accountname.blob.core.windows.net>")
        {
            IsRequired = true,
            Arity = ArgumentArity.ExactlyOne
        };

        private readonly Option<string?> _pathTemplate = new (
            aliases: new[] { "--path-template", "-t" },
            () => "ams-migration-output/${ContainerName}/",
            description: @"Path template to determine the final path in the storage where files are uploaded.
Below are valid keyword that can be put in the path template:
    ${AssetName}     for input asset's name.
    ${AssetId}       for the Guid of the assetId.
    ${ContainerName} for the container name of the input asset.
    ${AlternateId}   for the alternative id of the input asset, use AssetName if AlternateId is not set.
e.g., ams-migration-output/${AssetName}/ will upload to a container named 'ams-migration-output' with path beginning with the input asset name.")
        {
            Arity = ArgumentArity.ZeroOrOne
        };
                
        private readonly Option<string?> _outputManifest = new Option<string?>(
            aliases: new[] { "--output-manifest-name", "-m" },
            description: @"The output manifest name without extension,
if it is not set, use input asset's manifest name.")
                    {
                        Arity = ArgumentArity.ZeroOrOne
                    };

        private readonly Option<DateTimeOffset?> _creationTimeStart = new Option<DateTimeOffset?>(
            aliases: new[] { "--creation-time-start", "-cs" },
            description: @"The earliest creation time of the selected assets in UTC, 
format is yyyy-MM-ddThh:mm:ssZ, the hh:mm:ss is optional.")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        private readonly Option<DateTimeOffset?> _creationTimeEnd = new Option<DateTimeOffset?>(
            aliases: new[] { "--creation-time-end", "-ce" },
            description: @"The latest creation time of the selected assets in UTC, 
format is yyyy-MM-ddThh:mm:ssZ, the hh:mm:ss is optional.")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        private readonly Option<string?> _filter = new Option<string?>(
            aliases: new[] { "--resource-filter", "-f" },
            description: @"An ODATA condition to filter the resources.
e.g.: ""name eq 'asset1'"" to match an asset with name 'asset1'.
Visit https://learn.microsoft.com/en-us/azure/media-services/latest/filter-order-page-entities-how-to for more information.")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        private readonly Option<Packager> _packagerType = new(
    aliases: new[] { "--packager" },
    () => Packager.Shaka,
    description: "The packager to use.")
        {
            IsHidden = true,
            IsRequired = false
        };

        private readonly Option<string?> _workingDirectory = new(
            aliases: new[] { "--working-dir" },
            () => Path.Combine(Path.GetTempPath(), "AMSMigrate"),
            description: @"The working directory to use for temporary files during packaging.")
        {
            IsRequired = false
        };

        private readonly Option<bool> _skipMigrated = new(
            aliases: new[] { "--skip-migrated" },
            () => true,
            description: @"Skip assets that have been migrated already.");

        private readonly Option<bool> _copyNonStreamable = new(
            aliases: new[] { "--copy-nonstreamable" },
            () => true,
            description: @"Copy non-streamable assets (Assets without .ism file) as is.");

        private readonly Option<bool> _overwrite = new(
            aliases: new[] { "-y", "--overwrite" },
            () => true,
            description: @"Overwrite the files in the destination.");

        const int DefaultBatchSize = 1;
        private readonly Option<int> _batchSize = new(
            aliases: new[] { "--batch-size", "-b" },
            () => DefaultBatchSize,
            description: @"Batch size for parallel processing.");

        const int SegmentDurationInSeconds = 2;

        public AssetOptionsBinder()
        {
            _batchSize.AddValidator(result =>
            {
                var value = result.GetValueOrDefault<int>();
                if (value < 1 || value > 10)
                {
                    result.ErrorMessage = "Invalid batch size. Only values 1..10 are supported";
                }
            });

            _pathTemplate.AddValidator(result => {
                var value = result.GetValueOrDefault<string>();
                if (!string.IsNullOrEmpty(value))
                {
                    var (ok, key) = TemplateMapper.Validate(value, TemplateType.Assets);
                    if (!ok)
                    {
                        result.ErrorMessage = $"Invalid template: {value}. Template key '{key}' is invalid.";
                    }
                }
            });
        }

        public Command GetCommand(string name, string description)
        {
            var command = new Command(name, description);
            command.AddOption(_sourceAccount);
            command.AddOption(_storageAccount);
            command.AddOption(_pathTemplate);
            command.AddOption(_outputManifest);
            command.AddOption(_creationTimeStart);
            command.AddOption(_creationTimeEnd);
            command.AddOption(_filter);
            command.AddOption(_overwrite);
            command.AddOption(_skipMigrated);
            command.AddOption(_packagerType);
            command.AddOption(_workingDirectory);
            command.AddOption(_copyNonStreamable);
            command.AddOption(_batchSize);
            return command;
        }

        protected override AssetOptions GetBoundValue(BindingContext bindingContext)
        {
            var workingDirectory = bindingContext.ParseResult.GetValueForOption(_workingDirectory)!;
            Directory.CreateDirectory(workingDirectory);
            return new AssetOptions(
                bindingContext.ParseResult.GetValueForOption(_sourceAccount)!,               
                bindingContext.ParseResult.GetValueForOption(_storageAccount)!,
                bindingContext.ParseResult.GetValueForOption(_packagerType),
                bindingContext.ParseResult.GetValueForOption(_pathTemplate)!,
                bindingContext.ParseResult.GetValueForOption(_outputManifest)!,
                bindingContext.ParseResult.GetValueForOption(_creationTimeStart),
                bindingContext.ParseResult.GetValueForOption(_creationTimeEnd),
                bindingContext.ParseResult.GetValueForOption(_filter),
                workingDirectory,
                bindingContext.ParseResult.GetValueForOption(_copyNonStreamable),
                bindingContext.ParseResult.GetValueForOption(_overwrite),
                bindingContext.ParseResult.GetValueForOption(_skipMigrated),
                SegmentDurationInSeconds,
                bindingContext.ParseResult.GetValueForOption(_batchSize)
            );
        }

        public AssetOptions GetValue(BindingContext bindingContext) => GetBoundValue(bindingContext);
    }
}
