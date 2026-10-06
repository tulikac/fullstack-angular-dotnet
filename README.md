# Angular and ASP.NET Core full-stack sample

This repository tests a Builder Apps deployment with two components behind one
hostname:

- An Angular 21 static frontend at `/`
- An ASP.NET Core 10 API at `/api`

The browser calls relative `/api` URLs, so deployment does not require CORS
configuration.

## Local setup

Install frontend dependencies:

```powershell
Set-Location web
npm ci
```

Restore the API and tests:

```powershell
Set-Location ..
dotnet restore FullstackAngularDotnet.slnx
```

## Run locally

Start the API:

```powershell
Set-Location api\FullstackAngularDotnet.Api
$env:PORT = "18093"
dotnet run --no-launch-profile
```

In another terminal, start Angular with the local API proxy:

```powershell
Set-Location web
npm start -- --host 127.0.0.1 --port 18092 --proxy-config proxy.conf.json
```

Run the combined route checks:

```powershell
.\scripts\verify.ps1 -BaseUrl http://127.0.0.1:18092
```

## Production validation

```powershell
Set-Location web
npm run build

Set-Location ..
dotnet test FullstackAngularDotnet.slnx --configuration Release
dotnet publish api\FullstackAngularDotnet.Api\FullstackAngularDotnet.Api.csproj --configuration Release
```

## Builder Apps configuration

The Angular component uses an explicit install/build/copy command because a
custom Oryx build command must install dependencies and copy its artifacts
beneath `$DESTINATION_DIR`. The API is pinned to the Builder-supported .NET
`10.0.12` runtime and listens on the injected `PORT`.

