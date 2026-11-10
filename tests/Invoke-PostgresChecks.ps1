$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$testSuffix = [Guid]::NewGuid().ToString('N')
$networkName = "coordinates-checks-$testSuffix"
$containerName = "coordinates-postgres-$testSuffix"
$testPassword = [Guid]::NewGuid().ToString('N')
$networkCreated = $false
$containerCreated = $false

function Assert-DockerSuccess
{
    if ($LASTEXITCODE -ne 0)
    {
        throw "Docker command failed with exit code $LASTEXITCODE."
    }
}

try
{
    & docker network create $networkName | Out-Null
    Assert-DockerSuccess
    $networkCreated = $true
    & docker run --detach --rm --name $containerName --network $networkName --network-alias database `
        --env POSTGRES_DB=coordinates_checks --env "POSTGRES_PASSWORD=$testPassword" postgres:17 | Out-Null
    Assert-DockerSuccess
    $containerCreated = $true
    $ready = $false

    for ($attempt = 0; $attempt -lt 30; $attempt++)
    {
        & docker exec $containerName pg_isready --host 127.0.0.1 --username postgres --dbname coordinates_checks *> $null

        if ($LASTEXITCODE -eq 0)
        {
            $ready = $true
            break
        }

        Start-Sleep -Seconds 1
    }

    if (-not $ready)
    {
        throw 'The disposable PostgreSQL instance did not become ready.'
    }

    & docker run --rm --volume "${repositoryRoot}:/src" --workdir /src mcr.microsoft.com/dotnet/sdk:8.0 `
        dotnet build CoordinatesTinyApp.sln --configuration Release
    Assert-DockerSuccess
    & docker run --rm --network $networkName --env COORDINATES_TEST_PGHOST=database `
        --env "COORDINATES_TEST_PGPASSWORD=$testPassword" --volume "${repositoryRoot}:/src" --workdir /src `
        mcr.microsoft.com/dotnet/sdk:8.0 dotnet run --project tests/CoordinatesTinyApp.Checks `
        --configuration Release --no-build -- --postgres-fixture
    Assert-DockerSuccess
}
finally
{
    if ($containerCreated)
    {
        & docker rm --force $containerName | Out-Null
    }

    if ($networkCreated)
    {
        & docker network rm $networkName | Out-Null
    }
}
