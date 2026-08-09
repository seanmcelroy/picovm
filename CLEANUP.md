# PicoVM Cleanup Backlog

This document catalogues all confirmed code quality issues found in the PicoVM codebase, organized by severity. Each entry describes the problem and suggests a concrete fix.

---

## 1. Bugs (will crash or silently corrupt output)

### 1.1 64-bit BSS address truncated to 4 bytes
**File:** `picovm/Assembler/BytecodeCompiler.cs:466-469`

In the `else` branch (handling `TAddrSize == UInt64`), the BSS placeholder validation loop runs only 4 iterations (`i < 4`) and the subsequent `Array.Copy` copies only 4 bytes from an 8-byte `BitConverter.GetBytes(UInt64)` result. The upper 32 bits of every 64-bit BSS symbol address are silently zeroed.

**Fix:** Change both the loop bound and the copy length from `4` to `8` (or `sizeof(ulong)`) in the `UInt64` branch.

```csharp
// Before
for (int i = 0; i < 4; i++) { ... }
Array.Copy(BitConverter.GetBytes((UInt64)bssOffset), (long)0, textSegment, ..., 4);

// After
for (int i = 0; i < 8; i++) { ... }
Array.Copy(BitConverter.GetBytes((UInt64)bssOffset), (long)0, textSegment, ..., 8);
```

---

### 1.2 Add() 8-byte case reads only 4 bytes, throws at runtime
**File:** `picovm/Assembler/BytecodeCompiler.cs:1142`

Inside the `typeHintSize == 8` branch of `Add()`, the code calls `BitConverter.GetBytes(operand2.ParseUInt32Constant())`, producing a 4-byte array, then passes `8` as the copy length to `Array.Copy`. This throws `ArgumentException: Offset and length were out of bounds` for any 64-bit `ADD REG CON` instruction.

**Fix:** Use `ParseUInt64Constant()` so the source buffer is 8 bytes.

```csharp
// Before
Array.Copy(BitConverter.GetBytes(operand2.ParseUInt32Constant()), 0, ret, 2, 8);

// After
Array.Copy(BitConverter.GetBytes(operand2.ParseUInt64Constant()), 0, ret, 2, 8);
```

---

### 1.3 And() 8-byte case has the same crash as Add()
**File:** `picovm/Assembler/BytecodeCompiler.cs:1216`

Identical to finding 1.2: `ParseUInt32Constant()` produces 4 bytes but `Array.Copy` requests 8, crashing at runtime for any 64-bit `AND REG CON` instruction.

**Fix:** Same as 1.2 — use `ParseUInt64Constant()`.

```csharp
// Before
Array.Copy(BitConverter.GetBytes(operand2.ParseUInt32Constant()), 0, ret, 2, 8);

// After
Array.Copy(BitConverter.GetBytes(operand2.ParseUInt64Constant()), 0, ret, 2, 8);
```

---

### 1.4 ADD_MEM_CON case 8 advances instruction pointer by 4 instead of 8
**File:** `picovm/VM/Agent64.cs:251`

In the `case 8:` branch of the `ADD_MEM_CON` handler, `operand2value` is read with `BitConverter.ToUInt64` (consuming 8 bytes), but `instructionPointer` is only incremented by 4. Every other size case advances by the matching number of bytes (`case 4` → `+= 4`, `case 2` → `+= 2`, `case 1` → `+= 1`). This leaves the instruction pointer pointing 4 bytes into the operand, corrupting the decoding of every subsequent instruction in any 64-bit program that uses this instruction.

**Fix:** Change `instructionPointer += 4` to `instructionPointer += 8` in the `case 8:` branch.

```csharp
// Before
case 8:
    operand2value = BitConverter.ToUInt64(memory, (int)instructionPointer);
    instructionPointer += 4;  // wrong

// After
case 8:
    operand2value = BitConverter.ToUInt64(memory, (int)instructionPointer);
    instructionPointer += 8;
```

---

### 1.5 All CalculateRoundUpTo16Pad overloads are hardcoded to return 0
**File:** `picovm/Packager/Elf/ElfUtility.cs:61-64`

All four overloads of `CalculateRoundUpTo16Pad` have `=> 0` as their body with the real rounding logic commented out on the same line. Every caller (both ELF packagers and loaders) always receives zero padding, meaning ELF segments are never rounded up to alignment boundaries. This is a silent correctness defect.

**Fix:** Uncomment the real logic that is already inline on those lines.

```csharp
// Before
public static int CalculateRoundUpTo16Pad(this uint? realSize, uint roundUp = 16) => 0;
// => realSize == 0 ? 0 : realSize!.Value % roundUp == 0 ? 0 : (int)(roundUp - (realSize.Value % roundUp));

// After
public static int CalculateRoundUpTo16Pad(this uint? realSize, uint roundUp = 16)
    => realSize == null || realSize == 0 ? 0
     : realSize.Value % roundUp == 0 ? 0
     : (int)(roundUp - (realSize.Value % roundUp));
```

Apply the equivalent fix to the other three overloads.

---

### 1.6 PE import table bounds guard is always true
**File:** `picovm/Packager/PE/LoaderPE.cs:111, 149`

`PEDataDictionaryIndex.IMPORT_TABLE == 1`, so the guard `rvaAndSizes.Count + 1 >= 1` simplifies to `Count >= 0`, which is always true for any `List<T>`. The subsequent direct index at line 113 uses `rvaAndSizes[1]`, which requires `Count >= 2`. An `IndexOutOfRangeException` is possible on a PE binary with a short data directory. The same problem recurs at line 149 for `RESOURCE_TABLE` (== 2).

**Fix:** Use a strict greater-than comparison against the index being accessed.

```csharp
// Before
if (rvaAndSizes.Count + 1 >= (int)PEDataDictionaryIndex.IMPORT_TABLE)

// After
if (rvaAndSizes.Count > (int)PEDataDictionaryIndex.IMPORT_TABLE)
```

Apply the same pattern at line 149 for `RESOURCE_TABLE`.

---

## 2. Dead Code / Unused Infrastructure

### 2.1 Macro infrastructure is parsed but never expanded
**File:** `picovm/Assembler/BytecodeCompiler.cs:64-109`

Lines 64–109 build up a `macros` list from `%macro`/`%endmacro` directives. After the preprocessing loop ends at line 159, `macros` is never referenced again — it is not passed to `CompileTextSectionLinesToBytecode`, not iterated for expansion, and not used anywhere else. Any macro invocation in a `.text` section reaches the catch-all `throw new Exception("ERROR: Cannot compile: ...")`.

**Fix:** Either implement macro expansion (pass `macros` into `CompileTextSectionLinesToBytecode` and expand invocations before instruction parsing), or remove the dead parsing code and reject `%macro` directives with a clear "not implemented" error.

---

### 2.2 Linux64Kernel overrides are byte-for-byte copies of the base class
**File:** `picovm/VM/Linux64Kernel.cs:8`

`HandleInterrupt`, `sys_read`, and `sys_write` in `Linux64Kernel` are verbatim duplicates of the corresponding methods in `Linux32Kernel`. Since `Linux64Kernel` extends `Linux32Kernel`, deleting all three method bodies would leave the subclass using the inherited behavior — which is identical. The inheritance relationship currently provides no benefit.

**Fix:** Delete the three redundant method bodies from `Linux64Kernel`. If 64-bit syscall semantics need to diverge from 32-bit in the future, add only the differing parts at that point.

---

### 2.3 Errors enum is defined but never referenced
**File:** `picovm/VM/Linux32Kernel.cs:9-13`

The nested `Errors` enum (`EBADF = 9`, `EINVAL = 22`) has no call sites anywhere in the project. The TODO comments at lines 78 and 114 reference errno handling as a future concern but never use the enum. It was declared in anticipation of functionality that was never completed.

**Fix:** Delete the `Errors` enum. If errno handling is implemented later, re-introduce it at that time.

---

### 2.4 GnuHash method has no call sites
**File:** `picovm/Packager/Elf/ElfUtility.cs:66`

The `GnuHash` extension method is defined but never called anywhere in the project.

**Fix:** Delete the method, or move it to a separate file clearly marked as "not yet wired up" if it is needed for a planned feature.

---

### 2.5 PEImportNameHintTable class is never used
**File:** `picovm/Packager/PE/PEImportNameHintTable.cs`

`PEImportNameHintTable` is a thin subclass of `List<PEImportNameHintEntry>` with no body. No other file instantiates or references it. `LoaderPE.cs` uses `PEImportNameHintEntry` directly.

**Fix:** Delete the file.

---

### 2.7 Resource table stub seeks but reads nothing
**File:** `picovm/Packager/PE/LoaderPE.cs:149-153`

The block locates the resource table section, seeks the stream to it, then exits the block without reading or storing any data. The stream is left positioned mid-section, which would confuse any future read after this block. This is incomplete stub code.

**Fix:** Either implement resource table parsing or remove the block entirely. If it is a placeholder, at minimum `SeekToRVA` should not be called, or the stream should be reset afterward.

---

### 2.8 Commented-out incorrect implementation in PEImportLookupEntry
**File:** `picovm/Packager/PE/PEImportLookupEntry.cs:17-19`

Lines 17–19 are a commented-out block using wrong bit-masks from an earlier, incorrect implementation attempt. It serves no documentation value.

**Fix:** Delete the commented-out lines.

---

### 2.9 verbosity is always 1 — dead code block in Program.cs
**File:** `picovm/Program.cs:573, 592-605`

`var verbosity = 1` is set and never reassigned. `if (verbosity > 1)` is permanently false, making the per-DLL import name loop and ordinal-count report at lines 593–605 unreachable.

**Fix:** Either wire `verbosity` to a `--verbose` command-line flag, or remove the dead block and the `verbosity` variable.

---

### 2.10 Commented-out debug call in execution loop
**File:** `picovm/Program.cs:262`

`//agent.Dump();` inside the `do { ... } while` loop is residual debug noise.

**Fix:** Delete the commented-out line. If register dumping is useful for diagnostics, surface it behind a proper `--debug` flag.

---

### 2.11 const uint hi = 0 makes the high-word shift a permanent no-op
**File:** `picovm/VM/Agent.cs:245`

In `WriteExtendedRegister(ulong[], Register, uint)`, `const uint hi = 0` is declared and every case uses `(ulong)hi << 32 | lo`. Because `hi` is always zero, the shift always produces zero and every assignment reduces to `registers[R_X] = value`. The `hi` constant and the shift are misleading clutter.

**Fix:** Remove the `hi` constant and simplify each assignment to `registers[R_X] = value` directly.

---

### 2.12 Unreachable throw statements (7 instances)

The following `throw` statements follow `switch` blocks where every arm already exits via `continue` or `throw`, leaving the code after the switch unreachable. These are likely copy-paste artifacts from a switch-less version of the code.

| File | Line | Location |
|------|------|----------|
| `BytecodeCompiler.cs` | 795 | After `VariableAddress` MOV inner switch |
| `BytecodeCompiler.cs` | 801 | After MOV outer switch |
| `BytecodeCompiler.cs` | 844 | After PUSH switch |
| `BytecodeCompiler.cs` | 1254 | End of `And()` |
| `BytecodeCompiler.cs` | 1286 | End of `XOr()` |
| `Linux32Kernel.cs` | 82 | After `sys_read` switch |
| `Linux64Kernel.cs` | 67 | After `sys_read` switch |

**Fix:** Delete each unreachable `throw`. The compiler should already be issuing warnings for these (`CS0162: Unreachable code detected`).

---

## 3. Unused Variables

### 3.1 o1Reg and o2Reg computed but never read in XOr()
**File:** `picovm/Assembler/BytecodeCompiler.cs:1266, 1271`

`o1Reg` and `o2Reg` are assigned by dictionary lookups but never referenced. Lines 1274–1275 perform the identical lookups again to fill `ret[1]` and `ret[2]`.

**Fix:** Remove the dead variable assignments and use `o1Reg`/`o2Reg` in the byte assignments instead of repeating the lookups.

```csharp
// Before
var o1Reg = registers[operand1.ToUpperInvariant()];
// ... other code ...
var o2Reg = registers[operand2.ToUpperInvariant()];
// ... other code ...
ret[1] = (byte)registers[operand1.ToUpperInvariant()];
ret[2] = (byte)registers[operand2.ToUpperInvariant()];

// After
var o1Reg = registers[operand1.ToUpperInvariant()];
var o2Reg = registers[operand2.ToUpperInvariant()];
ret[1] = (byte)o1Reg;
ret[2] = (byte)o2Reg;
```

---

### 3.2 PUSH_CON: operand assigned but never read (Agent.cs and Agent64.cs)
**Files:** `picovm/VM/Agent.cs:853`, `picovm/VM/Agent64.cs:668`

In both files, `var operand = (Register)memory[instructionPointer]` is assigned but never referenced. The very next line reads the same offset again as a `uint`/`ulong` into `val`, which is what actually gets pushed. The `operand` variable shadows the true intent of the instruction encoding.

**Fix:** Delete the unused `operand` assignment in both files.

---

### 3.3 ret in sys_write is always false and never mutated (two files)
**Files:** `picovm/VM/Linux32Kernel.cs:100`, `picovm/VM/Linux64Kernel.cs:86`

`var ret = false;` is declared, never changed, and returned verbatim from both the STDOUT and STDERR arms of the switch.

**Fix:** Remove the variable and replace each `return ret;` with `return false;` directly.

---

### 3.4 generateSectionHeaderTable stored but never consulted
**Files:** `picovm/Packager/Elf/Elf32/PackagerElf32.cs:14`, `picovm/Packager/Elf/Elf64/PackagerElf64.cs:14`

Both packager classes accept a `generateSectionHeaderTable` constructor parameter (defaulting to `true`) and store it in a private field, but `Write()` unconditionally emits the section header table regardless of the flag's value. Passing `false` has no effect.

**Fix:** Either use the field in `Write()` to conditionally skip section header emission, or remove the parameter, the field, and all constructor call sites that pass it.

---

### 3.5 Unused StringBuilder in ConvertInfixToReversePolishNotation
**File:** `picovm/Assembler/CompilerDataAllocationDirective.cs:79`

`var sb = new StringBuilder()` is declared inside the foreach loop, never appended to, and never read. All token construction in the surrounding code uses direct `token.Substring(...)` calls.

**Fix:** Delete the `sb` declaration. Also remove `using System.Text` at line 5, which exists solely for this dead variable (see §4.2).

---

### 3.6 Return values from Assemble() and Execute() are silently discarded
**File:** `picovm/Program.cs:46, 73, 89, 91`

In the `asm`, `run`, and `asmrun` command branches, the return values from `Assemble()` and `Execute()` are stored in local variables (`compilation`, `result`) that are never read. Error codes and compilation metadata produced by these calls are thrown away.

**Fix:** Either propagate the return values (for example, use the exit code from `Execute()` as the process exit code), or discard them explicitly with `_` to communicate the intent.

---

## 4. Unused `using` Directives

All of the following using directives can be deleted with no effect on compilation.

| File | Line | Directive | Reason unused |
|------|------|-----------|---------------|
| `Assembler/CompilerDataAllocationDirective.cs` | 3 | `using System.Globalization` | No `CultureInfo`, `NumberStyles`, etc. appear in the file |
| `Assembler/CompilerDataAllocationDirective.cs` | 5 | `using System.Text` | Exists only for the dead `StringBuilder sb` (see §3.5) |
| `Assembler/CompilationResultBase.cs` | 4 | `using System.Linq` | `ToImmutableList()` comes from `System.Collections.Immutable`, not LINQ |
| `VM/ILoader.cs` | 2 | `using picovm.VM` | Self-import; file already declares `namespace picovm.VM` |
| `VM/ExecutionResult.cs` | 1 | `using System` | No bare `System.*` types used; primitives are C# keywords |
| `Packager/InspectionResult.cs` | 3 | `using System.IO` | No `Stream` or other IO type appears in this file |
| `Packager/Elf/ElfUtility.cs` | 3 | `using System.Text` | No `StringBuilder` or `Encoding` types appear |
| `Packager/PE/LoaderPE.cs` | 5 | `using System.Linq` | No LINQ operators used; `ToImmutableList()` comes from `System.Collections.Immutable` |
| `picovm.Tests/BytecodeCompilerTest.cs` | 2 | `using picovm.VM` | No `picovm.VM` types referenced in this test file |

**Fix:** Delete each listed directive. Running `dotnet build` after each deletion confirms nothing breaks.

---

## 5. Poor Practices

### 5.1 readonly struct Macro holds a mutable List<string>
**File:** `picovm/Assembler/Macro.cs:1, 9`

`readonly struct Macro` declares `public readonly List<string> MacroLines`. The `readonly` modifier on a struct only prevents reassignment of the struct variable itself — it does not prevent callers from mutating the `List<string>` held inside. `BytecodeCompiler.cs:108` exploits this with `macros.Last().MacroLines.Add(line)`. The `readonly struct` declaration falsely implies deep immutability.

**Fix:** Change the field type to `ImmutableArray<string>` (or `IReadOnlyList<string>`) and update the population code to build the list externally before constructing the `Macro`.

---

### 5.2 LoaderResult64 implements the non-generic ILoaderResult
**File:** `picovm/VM/LoaderResult64.cs:7`

`LoaderResult64` implements the non-generic `ILoaderResult` instead of `ILoaderResult<UInt64>`. Additionally, `EntryPoint` is a bare public field rather than a property, inconsistent with `LoaderResult32` which correctly implements `ILoaderResult<UInt32>` and exposes `EntryPoint` as a `{ get; private set; }` property. Code using the generic `ILoaderResult<TAddrSize>` interface cannot accept a `LoaderResult64`.

**Fix:** Change the class declaration to implement `ILoaderResult<UInt64>` and convert the `EntryPoint` field to a property to match `LoaderResult32`.

---

### 5.4 Magic number -666 used as unknown-bytecode sentinel
**Files:** `picovm/VM/Agent.cs:980`, `picovm/VM/Agent64.cs:795`, `picovm/Program.cs:266`

The value `-666` is returned from the default case of `Tick()` and matched with `case -666:` in `Program.cs`, with no named constant explaining its meaning. The value is duplicated across three files.

**Fix:** Define a named constant in a shared location (e.g., in `Bytecode.cs` or a new `AgentError.cs`):

```csharp
public static class AgentError
{
    public const int UnknownBytecode = -666;
}
```

Then replace all three bare `-666` literals with `AgentError.UnknownBytecode`.

---

### 5.6 Null check on a ValueType (struct) is always false
**File:** `picovm/Assembler/BytecodeCompiler.cs:479`

`entryPoint` is declared as `ValueType` and initialized to either `(ValueType)(UInt32)0` or `(ValueType)(UInt64)0`. Structs cannot be `null` when boxed this way via a non-nullable assignment path, so `if (entryPoint == null)` is always false and the throw is unreachable.

**Fix:** Remove the null check. If a "not set" state is genuinely needed, consider using `ValueType?` (nullable) or a separate `bool entryPointSet` flag.

---

### 5.9 Header32.TryRead silently swallows all exceptions
**File:** `picovm/Packager/Elf/Elf32/Header32.cs:74`

The catch block is `catch (Exception) { header = default; return false; }` — the exception is discarded with no logging. The equivalent `Header64.TryRead` (line 76) correctly captures the exception and logs it to `Console.Error` before returning false. Silent failure makes it impossible to diagnose parse errors during file-type detection.

**Fix:** Capture and log the exception to match `Header64.TryRead`:

```csharp
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    header = default;
    return false;
}
```

---

### 5.10 LoaderElf64 uses old-style manual null guard
**File:** `picovm/Packager/Elf/Elf64/LoaderElf64.cs:15`

`LoaderElf64` uses `if (stream == null) throw new ArgumentNullException(nameof(stream));` while `LoaderElf32` uses the idiomatic `ArgumentNullException.ThrowIfNull(stream)` (.NET 6+ API). Inconsistency makes the guard harder to read and maintain.

**Fix:**
```csharp
// Before
if (stream == null) throw new ArgumentNullException(nameof(stream));

// After
ArgumentNullException.ThrowIfNull(stream);
```

---

### 5.11 Debug scaffolding left in Assemble()
**File:** `picovm/Program.cs:140-143`

Lines 140–143 contain `// DEBUG`, then `System.IO.File.Delete(output)`, followed by a commented-out `//return -4;`. The delete silently clobbers any pre-existing output file instead of returning an error. This was clearly temporary scaffolding never cleaned up.

**Fix:** Replace the silent delete with a proper error path:

```csharp
// Before
// DEBUG
System.IO.File.Delete(output);
//return -4;

// After
Console.Error.WriteLine($"ERROR: Output file already exists: {output}");
return -4;
```

---

### 5.12 Redundant fs.Flush() and fs.Close() inside a using block
**File:** `picovm/Program.cs:198-199`

`fs.Flush()` and `fs.Close()` are called explicitly immediately before the `using` block's closing brace. The `using` statement calls `Dispose()` on exit, which already flushes buffered data and closes the file handle. Both calls are superfluous.

**Fix:** Delete lines 198 and 199. The `using` block handles cleanup correctly.

---

### 5.13 Fragile relative path in two compiler tests
**File:** `picovm.Tests/BytecodeCompilerTest.cs:47, 57`

`CompileLogicalInstructionsAsm` and `CompileReadKeyboardAsm` pass a raw relative path to `compiler.Compile(sourceFileName)` while the `File.Exists` guard on the preceding line uses `Path.Combine(Environment.CurrentDirectory, sourceFileName)`. The other three tests in the same file consistently use `Path.Combine(...)` for both the guard and the compile call.

**Fix:** Pass `Path.Combine(Environment.CurrentDirectory, sourceFileName)` to `compiler.Compile(...)` in both tests, matching the rest of the class.

