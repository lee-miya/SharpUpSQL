# SharpUpSQL

C# port of [NetSPI PowerUpSQL](https://github.com/NetSPI/PowerUpSQL) for Windows (.NET Framework 4.8). SharpUpSQL exposes the same cmdlet-style commands as a standalone CLI, using `System.Data.SqlClient` with no SMO or SQLPS dependency.

## Features

- **108 PowerUpSQL commands** — discovery, enumeration, audit, escalation, OS command execution, AD recon, persistence, and password recovery (4 fuzz commands remain on the roadmap; see [docs/FUNCTION_PARITY.md](docs/FUNCTION_PARITY.md))
- **Phase 6 enhancements** — NTLM pass-the-hash (`-Hash`), linked-server chain queries, RPC enable/disable, JSON pipeline I/O, custom TCP port, advanced connection options, debug SQL preview, comma-separated multi-host targets
- **Lab-validated parity** — regression suite against an isolated SQL Server lab; golden snapshots from PowerUpSQL for diffing

## Requirements

| Requirement | Notes |
|-------------|-------|
| Windows 10/11 or Server 2019+ | .NET Framework 4.8 (in-box on supported Windows) |
| SQL Server lab (optional) | For integration/regression tests — see [lab/README.md](lab/README.md) |
| PowerShell 5.1+ | Build script and regression runner |

## Build

From the repository root:

```powershell
.\build.ps1
```

Output: `artifacts\Release\SharpUpSQL.Cli\SharpUpSQL.exe` (plus `SharpUpSQL.dll`, `SharpUpSQL.Core.dll`).

Debug build:

```powershell
.\build.ps1 -Configuration Debug
```

## Usage

Commands mirror PowerUpSQL function names (case-insensitive):

```text
SharpUpSQL.exe Get-SQLInstanceLocal
SharpUpSQL.exe Get-SQLServerInfo -Instance localhost,1433 -Username sa -Password "YourPassword"
SharpUpSQL.exe Invoke-SQLAudit -Instance SQL01\INST -Verbose
SharpUpSQL.exe Get-SQLInstanceDomain | SharpUpSQL.exe Invoke-SQLAudit --stdin json
```

### Global options

| Option | Description |
|--------|-------------|
| `-Verbose` | Verbose progress messages |
| `-Debug` | Print T-SQL before execution |
| `-Port <n>` | Append TCP port to `-Instance` |
| `-Hash <ntlm>` | Pass-the-hash (requires `-Domain`, `-Username`) |
| `-ForceNamedPipe` | Connect via named pipe |
| `-PacketSize <n>` | TDS packet size |
| `-Instance h1,h2[,port]` | Comma-separated targets |
| `--stdin json` | Read pipeline objects from stdin |
| `--format json` | JSON output |

List all commands:

```text
SharpUpSQL.exe help
```

(Any unknown command name prints the full command list.)

### SharpUpSQL-only commands

| Command | Purpose |
|---------|---------|
| `Invoke-SQLLinkedChainQuery` | Multi-hop `EXEC ... AT [link]` / OPENQUERY chain |
| `Invoke-SQLEnableRpc` | Enable RPC on a linked server |
| `Invoke-SQLDisableRpc` | Disable RPC on a linked server |

## Testing

### Unit + command registry (no SQL Server required)

```powershell
.\tests\run-unit-tests.ps1
```

Validates core helpers (Luhn, instance parsing, linked-chain SQL builder, JSON pipeline) and confirms the CLI registers all PowerUpSQL commands except the four documented fuzz gaps.

### Full regression (build + unit + lab)

```powershell
.\tests\Run-Regression.ps1
```

Skips lab tests automatically if `lab\config\lab.settings.json` is missing. Use `-SkipLab` to run only build and unit tests:

```powershell
.\tests\Run-Regression.ps1 -SkipLab
```

### Lab setup

See [lab/README.md](lab/README.md). After setup:

```powershell
.\lab\scripts\Test-LabConnectivity.ps1
.\lab\scripts\Export-GoldenSnapshots.ps1   # PowerUpSQL baseline
.\tests\Run-LabRegression.ps1 -CompareSnapshots
```

## Command reference

Full PowerUpSQL ↔ SharpUpSQL mapping, implementation status, and lab profiles: **[docs/COMMAND_REFERENCE.md](docs/COMMAND_REFERENCE.md)** and **[docs/FUNCTION_PARITY.md](docs/FUNCTION_PARITY.md)**.

## Project layout

```text
SharpUpSQL/
├── src/
│   ├── SharpUpSQL.Core/     # Connection, auth, TDS/PtH, threading, JSON I/O
│   ├── SharpUpSQL/          # Command implementations (by PowerUpSQL category)
│   └── SharpUpSQL.Cli/      # CLI entry point and command registry
├── tests/
│   ├── SharpUpSQL.Tests/    # Unit + registry parity tests
│   └── Run-Regression.ps1   # Full regression entry point
├── lab/                     # SQL Server lab scripts and golden snapshots
├── docs/
│   ├── FUNCTION_PARITY.md   # Implementation and test status matrix
│   └── COMMAND_REFERENCE.md # User-facing command对照表
└── build.ps1
```

## Relationship to PowerUpSQL

SharpUpSQL targets behavioral parity with PowerUpSQL **v1.105.0** for the exported functions in `PowerUpSQL.psd1`. Parameter names, output fields, `-Exploit` paths, `-Threads` batching, and pipeline input follow the PowerShell module where feasible.

**Intentionally out of scope** (PowerUpSQL roadmap items not yet in the module): `Get-SQLExfil*`, `Get-SQLInstanceScanTCP`, Azure instance discovery.

## License

SharpUpSQL is provided under the **BSD 3-Clause License**, consistent with [PowerUpSQL](https://github.com/NetSPI/PowerUpSQL/blob/master/LICENSE):

```text
Copyright (c) SharpUpSQL contributors
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice,
   this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.
3. Neither the name of the copyright holder nor the names of its contributors
   may be used to endorse or promote products derived from this software
   without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
```

Use only on systems you are authorized to test. SharpUpSQL includes offensive-security capabilities; isolate lab environments and never point tools at production systems without explicit permission.

## Acknowledgments

- [NetSPI PowerUpSQL](https://github.com/NetSPI/PowerUpSQL) — original research and PowerShell implementation
- [SQLRecon](https://github.com/skahwah/SQLRecon), [SharpSQL](https://github.com/mlcsec/SharpSQL) — reference for C# patterns and enhancements
