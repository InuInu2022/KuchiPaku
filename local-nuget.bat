dotnet nuget add source local-nuget -n debug-repository
dotnet nuget push ..\FluentCeVIOWrapper\FluentCeVIOWrapper.Common\bin\Release\FluentCeVIOWrapper.0.1.0.nupkg -s debug-repository
dotnet nuget list source

rem dotnet nuget remove source debug-repository
rem dotnet nuget disable source debug-repository