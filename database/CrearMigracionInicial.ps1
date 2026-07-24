param(
    [string]$ConnectionString = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$web = Join-Path $root "src/TecAjalpan.Horarios.Web/TecAjalpan.Horarios.Web.csproj"
$infra = Join-Path $root "src/TecAjalpan.Horarios.Infrastructure/TecAjalpan.Horarios.Infrastructure.csproj"

if ($ConnectionString) {
    $env:ConnectionStrings__Horarios = $ConnectionString
}

dotnet tool restore
dotnet restore (Join-Path $root "TecAjalpan.Horarios.slnx")
dotnet ef migrations add Inicial `
    --project $infra `
    --startup-project $web `
    --output-dir Persistence/Migrations
dotnet ef database update `
    --project $infra `
    --startup-project $web
