# PicoVM Cleanup Backlog

This document catalogues confirmed code quality issues found in the PicoVM codebase, organized by severity. Each entry describes the problem and suggests a concrete fix.

---

## 1. Dead Code / Unused Infrastructure

### 1.1 Macro infrastructure is parsed but never expanded
**File:** `picovm/Assembler/BytecodeCompiler.cs:68-110`

The preprocessing loop builds up a `macros` list from `%macro`/`%endmacro` directives, but after the loop ends `macros` is never referenced again — it is not passed to `CompileTextSectionLinesToBytecode`, not iterated for expansion, and not used anywhere else. Any macro invocation in a `.text` section reaches the catch-all throw.

**Fix:** Either implement macro expansion (pass `macros` into `CompileTextSectionLinesToBytecode` and expand invocations before instruction parsing), or remove the dead parsing code and reject `%macro` directives with a clear "not implemented" error.

---

### 1.2 Errors enum is defined but never referenced
**File:** `picovm/VM/Linux32Kernel.cs:9-13`

The nested `Errors` enum (`EBADF = 9`, `EINVAL = 22`) has no call sites anywhere in the project. The TODO comments at lines 75 and 111 reference errno handling as a future concern but never use the enum. It was declared in anticipation of functionality that was never completed.

**Fix:** Delete the `Errors` enum. If errno handling is implemented later, re-introduce it at that time.

---

### 1.3 GnuHash method has no call sites
**File:** `picovm/Packager/Elf/ElfUtility.cs:64`

The `GnuHash` extension method is defined but never called anywhere in the project.

**Fix:** Delete the method, or move it to a separate file clearly marked as "not yet wired up" if it is needed for a planned feature.

---

### 1.4 PEImportNameHintTable class is never used
**File:** `picovm/Packager/PE/PEImportNameHintTable.cs`

`PEImportNameHintTable` is a thin subclass of `List<PEImportNameHintEntry>` with no body. No other file instantiates or references it. `LoaderPE.cs` uses `PEImportNameHintEntry` directly.

**Fix:** Delete the file.

---

### 1.5 Resource table stub seeks but reads nothing
**File:** `picovm/Packager/PE/LoaderPE.cs:142-146`

The block locates the resource table section, seeks the stream to it, then exits the block without reading or storing any data. The stream is left positioned mid-section, which would confuse any future read after this block. This is incomplete stub code.

**Fix:** Either implement resource table parsing or remove the block entirely. If it is a placeholder, at minimum `SeekToRVA` should not be called, or the stream should be reset afterward.

---

### 1.6 verbosity is always 1 — dead code block in Program.cs
**File:** `picovm/Program.cs:670, 689`

`var verbosity = 1` is set and never reassigned. `if (verbosity > 1)` is permanently false, making the per-DLL import name loop and ordinal-count report unreachable.

**Fix:** Either wire `verbosity` to a `--verbose` command-line flag, or remove the dead block and the `verbosity` variable.

---

### 1.7 const int hi = 0 makes the high-word shift a permanent no-op
**File:** `picovm/VM/Agent.cs:304`

In the `WriteExtendedRegister(ulong[], Register, int)` overload, `const int hi = 0` is declared and every case uses `hi << 32 | lo`. Because `hi` is always zero, the shift always produces zero and every assignment reduces to `registers[R_X] = (ulong)value`. The `hi` constant and the shift are misleading clutter. (The sibling `uint` overload no longer has this pattern — it was already cleaned up.)

**Fix:** Remove the `hi` constant and simplify each assignment to `registers[R_X] = (ulong)value` directly.

---

### 1.8 Unreachable throw statements

The following `throw` statements follow `switch` blocks where every arm already exits via `return` or `throw`, leaving the code after the switch unreachable.

| File | Line | Location |
|------|------|----------|
| `BytecodeCompiler.cs` | 1348 | End of `And()` — outer switch's `default` throws and the RegisterReference case's inner switch throws on every arm |
| `BytecodeCompiler.cs` | 1432 | End of `XOr()` — same structure as `And()` |
| `Linux32Kernel.cs` | 79 | End of `sys_read` — switch has STDIN (returns) and default (throws) |
| `Linux64Kernel.cs` | 65 | End of `sys_read` — same structure as 32-bit version |

Note: the trailing throws in `Add()` (line 1286) and `Cmp()` (line 1403) *are* reachable — those cases fall through to `break` when the inner switch doesn't match, so the trailing throw is the intended catch-all.

**Fix:** Delete each unreachable `throw`. The compiler should already be issuing warnings for these (`CS0162: Unreachable code detected`).

---

## 2. Unused Variables

### 2.1 ret in sys_write is always false and never mutated (two files)
**Files:** `picovm/VM/Linux32Kernel.cs:97`, `picovm/VM/Linux64Kernel.cs:83`

`var ret = false;` is declared, never changed, and returned verbatim from both the STDOUT and STDERR arms of the switch.

**Fix:** Remove the variable and replace each `return ret;` with `return false;` directly.

---

### 2.2 Return values from Assemble() and Execute() are silently discarded
**File:** `picovm/Program.cs:46, 73, 89, 91`

In the `asm`, `run`, and `asmrun` command branches, the return values from `Assemble()` and `Execute()` are stored in local variables (`compilation`, `result`) that are never read. Error codes and compilation metadata produced by these calls are thrown away.

**Fix:** Either propagate the return values (for example, use the exit code from `Execute()` as the process exit code), or discard them explicitly with `_` to communicate the intent.

---

## 3. Poor Practices

### 3.1 Fragile relative path in two compiler tests
**File:** `picovm.Tests/BytecodeCompilerTest.cs:46, 56`

`CompileLogicalInstructionsAsm` and `CompileReadKeyboardAsm` pass a raw relative path to `compiler.Compile(sourceFileName)` while the `File.Exists` guard on the preceding line uses `Path.Combine(Environment.CurrentDirectory, sourceFileName)`. The other three tests in the same file consistently use `Path.Combine(...)` for both the guard and the compile call.

**Fix:** Pass `Path.Combine(Environment.CurrentDirectory, sourceFileName)` to `compiler.Compile(...)` in both tests, matching the rest of the class.
