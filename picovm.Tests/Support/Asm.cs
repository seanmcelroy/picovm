using System.Collections.Generic;

namespace picovm.Tests.Support
{
    /// <summary>
    /// Builders for the boilerplate every assembly snippet in the test suite needs, so
    /// individual tests read as just the instructions under examination.
    /// </summary>
    public static class Asm
    {
        /// <summary>
        /// Wraps <paramref name="instructions"/> in a <c>.text</c> section with an entry
        /// point, terminating with <c>END</c> unless the caller already supplied one.
        /// </summary>
        public static string[] Text(params string[] instructions) =>
        [
            "section .text",
            "global _start",
            "_start:",
            .. Terminated(instructions)
        ];

        /// <summary>
        /// As <see cref="Text"/>, but preceded by a <c>.data</c> section built from
        /// <paramref name="dataDirectives"/>.
        /// </summary>
        /// <remarks>
        /// Every declared data symbol must be referenced by the code: the compiler reports
        /// "Data symbol X is not referenced in program code" as an error otherwise.
        /// </remarks>
        public static string[] WithData(string[] dataDirectives, params string[] instructions) =>
        [
            "section .data",
            .. dataDirectives,
            "section .text",
            "global _start",
            "_start:",
            .. Terminated(instructions)
        ];

        /// <summary>
        /// As <see cref="WithData"/>, but with a <c>.bss</c> section as well.
        /// </summary>
        public static string[] WithBss(string[] dataDirectives, string[] bssDirectives, params string[] instructions) =>
        [
            "section .data",
            .. dataDirectives,
            "section .bss",
            .. bssDirectives,
            "section .text",
            "global _start",
            "_start:",
            .. Terminated(instructions)
        ];

        /// <summary>
        /// A scratch address clear of both the text segment (loaded at 0) and the stack
        /// (which starts at the top of the 64KB memory and grows down).
        /// </summary>
        public const uint ScratchAddress = 0x1000;

        private static IEnumerable<string> Terminated(string[] instructions)
        {
            foreach (var instruction in instructions)
                yield return instruction;

            if (instructions.Length == 0 || !EndsProgram(instructions[^1]))
                yield return "END";
        }

        private static bool EndsProgram(string instruction) =>
            instruction.Split(';')[0].Trim().Equals("END", System.StringComparison.OrdinalIgnoreCase);
    }
}
