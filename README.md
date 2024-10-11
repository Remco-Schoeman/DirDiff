# DirDiff
A simple tool to compare 2 directory trees and list the differences.

## Usage
```powershell
C:\> dir_diff.exe -l "C:\path\to\left\dir" -r "C:\path\to\right\dir"
Left : C:\path\to\left\dir
Right: C:\path\to\right\dir

\de\UI.resources.dll : Hash Mismatch
        left  : A51057B4777448CF1513427E444FDC0FE0F028AB5879E79D89041C89B38D10DE
        right : A167D5D30CA2968D407CBF647C7747D784D266FDB074CFCD10C545B0BD2C4615

\nl\UI.resources.dll : Hash Mismatch
        left  : D69B47E8A8D2FA1FBBD7745AB7AEE20C25962C3DBBB050B8A7C3FB187BCFCBFB
        right : BE9D8D6846BE3FFD9CE844104DD3156B0DEF543B9083DD83B09771817025D385

... more output ...

\System.Diagnostics.DiagnosticSource.dll : Hash Mismatch
        left  : 19BA42737C1C0500373736968F3D15CB7897CB195049FD5F492E6FE1629DAAAB
        right : 7A953C41AEBD1F3E5D7AA32AE20B6773D7481206B61CB6FB8AE03F7A86B55113

  252 files in the left directory
  202 files in the right directory
  179 equal files
    8 hash mismatches
   65 files at the left with the right counterpart missing
   15 files at the right with the left counterpart missing
  267 files total
```

## Help

The `--mode` determines the printed results, by default the equal files are omitted.



```
C:\> DirDiff.exe -h

Description:
  DirDiff - Finds discrepancies in file trees by comparing SHA256 hashes of file contents

Usage:
  DirDiff [options]

Options:
  -l, --left <left> (REQUIRED)                                                         The left directory to compare
  -r, --right <right> (REQUIRED)                                                       The right directory to compare
  -m, --mode <All|Different|Equal|HashMismatch|LeftMissing|Missing|None|RightMissing>  Print only the selected results [default: Different]
  -f, --format <Csv|Default|Json|Text>                                                 Selects the output format [default: Default]
  --version                                                                            Show version information
  -?, -h, --help                                                                       Show help and usage information
```