@{
    RootModule = 'ObsBrowserSource.psm1'
    ModuleVersion = '1.0.1'
    GUID = 'e8891f92-45a9-4807-affd-d7512b417119'
    Author = 'YTLiveChat maintainers'
    Description = 'Starts OBS-aware backends and refreshes their matching Browser Source input.'
    PowerShellVersion = '5.1'
    CompatiblePSEditions = @('Desktop', 'Core')
    FunctionsToExport = @('Complete-BackendLaunch')
    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()
    PrivateData = @{
        PSData = @{
            Tags = @('OBS', 'BrowserSource', 'Launcher')
        }
    }
}
