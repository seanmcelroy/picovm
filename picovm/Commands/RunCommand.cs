using System.ComponentModel;
using System.Threading;
using picovm.Packager;
using Spectre.Console.Cli;

namespace picovm.Commands
{
    internal sealed class RunCommand : Command<RunCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<EXECUTABLE>")]
            [Description("File to run in the virtual machine.")]
            public string Executable { get; init; } = string.Empty;
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var type = Inspector.DetectPackageOutputType(settings.Executable);
            var loaded = Program.Load(settings.Executable, type);
            if (!loaded.Success)
                return -4;

            var result = Program.Execute(loaded);
            return result.Success ? 0 : -5;
        }
    }
}
