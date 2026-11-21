param(
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root "benchmark-results"
}
$project = Join-Path $root "CadSemanticAssistant.Benchmarks/CadSemanticAssistant.Benchmarks.csproj"
$dataset = Join-Path $root "CadSemanticAssistant.Benchmarks/TestData/benchmark_semantico.v1.json"
$catalog = Join-Path $root "CadSemanticAssistant.Plugin/Configuration/catalogo_bloques.v1.json"
$rules = Join-Path $root "CadSemanticAssistant.Plugin/Configuration/reglas_dominio.v1.json"

dotnet run --project $project -- --dataset $dataset --catalog $catalog --rules $rules --out $OutputDirectory
exit $LASTEXITCODE
