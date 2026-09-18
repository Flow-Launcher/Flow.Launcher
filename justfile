# Regenerate docs/API-Reference. Pass the path to your clone of https://github.com/Flow-Launcher/docs/
generate-api-docs docs_repo:
    dotnet run --project Flow.Launcher.DocsGen -c Release -- \
        Output/Release/Flow.Launcher.Plugin.dll \
        "{{docs_repo}}/API-Reference" \
        --clean
