# Use the .NET 9 SDK so the test runner and build tools are available
FROM mcr.microsoft.com/dotnet/sdk:9.0

WORKDIR /src

# Copy the repository into the image (build context should be the repo root)
COPY . .

# Restore the test project (adjust the .csproj path if your test project file name differs)
RUN dotnet restore "WebApplicationTeamCity.Tests/WebApplicationTeamCity.Tests.csproj"

# Expose a volume to receive test result files (TRX)
VOLUME /testresults

# Default action: run the tests, produce a TRX in /testresults and exit
ENTRYPOINT ["dotnet","test","WebApplicationTeamCity.Tests/WebApplicationTeamCity.Tests.csproj","-c","Release","--logger","trx;LogFileName=test_results.trx","--results-directory","/testresults","--no-restore","--verbosity","minimal"]