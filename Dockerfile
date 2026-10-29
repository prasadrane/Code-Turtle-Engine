# Multi-stage Dockerfile for Code Turtle Engine CLI
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CodeTurtleEngine.sln global.json ./
COPY src/CodeTurtleEngine.Core/CodeTurtleEngine.Core.csproj src/CodeTurtleEngine.Core/
COPY src/CodeTurtleEngine.Gatekeeper/CodeTurtleEngine.Gatekeeper.csproj src/CodeTurtleEngine.Gatekeeper/
COPY src/CodeTurtleEngine.Council/CodeTurtleEngine.Council.csproj src/CodeTurtleEngine.Council/
COPY src/CodeTurtleEngine.Llm/CodeTurtleEngine.Llm.csproj src/CodeTurtleEngine.Llm/
COPY src/CodeTurtleEngine.Cli/CodeTurtleEngine.Cli.csproj src/CodeTurtleEngine.Cli/
COPY src/CodeTurtleEngine.Web/CodeTurtleEngine.Web.csproj src/CodeTurtleEngine.Web/
COPY src/CodeTurtleEngine.Worker/CodeTurtleEngine.Worker.csproj src/CodeTurtleEngine.Worker/

COPY tests/CodeTurtleEngine.Core.Tests/CodeTurtleEngine.Core.Tests.csproj tests/CodeTurtleEngine.Core.Tests/
COPY tests/CodeTurtleEngine.Gatekeeper.Tests/CodeTurtleEngine.Gatekeeper.Tests.csproj tests/CodeTurtleEngine.Gatekeeper.Tests/
COPY tests/CodeTurtleEngine.Council.Tests/CodeTurtleEngine.Council.Tests.csproj tests/CodeTurtleEngine.Council.Tests/
COPY tests/CodeTurtleEngine.Llm.Tests/CodeTurtleEngine.Llm.Tests.csproj tests/CodeTurtleEngine.Llm.Tests/
COPY tests/CodeTurtleEngine.Cli.Tests/CodeTurtleEngine.Cli.Tests.csproj tests/CodeTurtleEngine.Cli.Tests/
COPY tests/CodeTurtleEngine.Web.Tests/CodeTurtleEngine.Web.Tests.csproj tests/CodeTurtleEngine.Web.Tests/
COPY tests/CodeTurtleEngine.Integration.Tests/CodeTurtleEngine.Integration.Tests.csproj tests/CodeTurtleEngine.Integration.Tests/
COPY samples/CodeTurtleEngine.CitationDemo/CodeTurtleEngine.CitationDemo.csproj samples/CodeTurtleEngine.CitationDemo/

RUN dotnet restore CodeTurtleEngine.sln

COPY . .

RUN dotnet publish src/CodeTurtleEngine.Cli/CodeTurtleEngine.Cli.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

ENTRYPOINT ["dotnet", "CodeTurtleEngine.Cli.dll"]
