using System.CommandLine;

namespace CallGraphGenerator;

class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("PlantUml Call Grapher");
        var rootCommand = SetupArguments();
        return await rootCommand.InvokeAsync(args);
    }

    private static RootCommand SetupArguments()
    {
        var fileArg = new Argument<FileInfo>(
            name: "file",
            description: "The project or solution file to process.");

        var namespaceArg = new Argument<string>(
            name: "namespace",
            description: "Namespace of entry point"
        );

        var classArg = new Argument<string>(
            name: "class",
            description: "Class of entry point"
        );

        var methodArg = new Argument<string>(
            name: "method",
            description: "Method entry point"
        );

        var outputFileOption = new Option<FileInfo?>(
            name: "--output",
            description: "Optional output file");

        var rootCommand = new RootCommand("PlantUml Call Grapher");
        rootCommand.AddArgument(fileArg);
        rootCommand.AddArgument(namespaceArg);
        rootCommand.AddArgument(classArg);
        rootCommand.AddArgument(methodArg);
        rootCommand.AddOption(outputFileOption);
        
        rootCommand.SetHandler(async (file, ns, cs, mt, outputFile) =>
        {
            var writer = Console.Out;
            try
            {
                if (outputFile != null)
                    writer = new StreamWriter(outputFile.FullName);

                var processor = new Processor(writer);
                await processor.OpenAsync(file.FullName);
                await processor.RunAsync(ns, cs, mt);

            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                throw;
            }
            finally
            {
                writer.Close();
            }
            
        }, fileArg, namespaceArg, classArg, methodArg, outputFileOption);

        return rootCommand;
    }
}