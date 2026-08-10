using System.ComponentModel;
using System.Threading;
using Spectre.Console.Cli;

namespace picovm.Commands
{
    internal sealed class AsmCommand : Command<AsmCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<OUTPUT>")]
            [Description("Resulting .elf output file (overwritten if it exists).")]
            public string Output { get; init; } = string.Empty;

            [CommandArgument(1, "<TYPE>")]
            [Description("Package type: elf32 or elf64.")]
            public string Type { get; init; } = string.Empty;

            [CommandArgument(2, "<FORMAT>")]
            [Description("Code/text format. Only 'pico' is supported.")]
            public string Format { get; init; } = string.Empty;

            [CommandArgument(3, "<INPUT>")]
            [Description("Source .asm assembly file.")]
            public string Input { get; init; } = string.Empty;

            [CommandOption("-n|--no-clobber")]
            [Description("Fail instead of overwriting an existing OUTPUT file.")]
            public bool NoClobber { get; init; }
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var compilation = Program.Assemble(settings.Output, settings.Type, settings.Format, settings.Input, settings.NoClobber);
            return compilation.Success ? 0 : -3;
        }
    }
}
