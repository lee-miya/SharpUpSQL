$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Join-Path $root 'PowerUpSQL.ps1.tmp'
$outB64 = Join-Path $root 'src\SharpUpSQL\Attack\XpDllTemplate.b64'
$outCs = Join-Path $root 'src\SharpUpSQL\Attack\XpDllTemplateData.cs'

$text = [IO.File]::ReadAllText($source)
if ($text -notmatch '\$DllBytes64\s*=\s*''([^'']+)''') {
    throw 'DllBytes64 not found in PowerUpSQL.ps1.tmp'
}

$b64 = $Matches[1]
[IO.File]::WriteAllText($outB64, $b64)

$cs = @"
namespace SharpUpSQL.Attack
{
    internal static class XpDllTemplateData
    {
        internal static byte[] GetTemplateBytes()
        {
            return System.Convert.FromBase64String(Base64);
        }

        private const string Base64 =
"@

# Split base64 into 80-char chunks for readable C# string concatenation
$chunkSize = 80
for ($i = 0; $i -lt $b64.Length; $i += $chunkSize) {
    $len = [Math]::Min($chunkSize, $b64.Length - $i)
    $chunk = $b64.Substring($i, $len)
    $cs += "`n            `"$chunk`" +"
}

$cs = $cs.TrimEnd(' +')
$cs += @"

;
    }
}
"@

[IO.File]::WriteAllText($outCs, $cs, [Text.UTF8Encoding]::new($false))
Write-Host "Wrote $($b64.Length) chars to XpDllTemplateData.cs"
