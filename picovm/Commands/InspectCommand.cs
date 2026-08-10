using System.ComponentModel;
using System.Threading;
using Spectre.Console.Cli;

namespace picovm.Commands
{
    internal sealed class InspectCommand : Command<InspectCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<EXECUTABLE>")]
            [Description("File to inspect.")]
            public string Executable { get; init; } = string.Empty;
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            Program.PrintInspection(settings.Executable);
            return 0;
        }
    }
}
