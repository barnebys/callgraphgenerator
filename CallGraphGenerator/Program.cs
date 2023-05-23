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

        var leafOpt = new Option<List<string>>(
            name: "--leaf",
            description: "Classes that should not be parsed further.");
        leafOpt.AddAlias("-l");
        
        var ignoreOpt = new Option<List<string>>(
            name: "--ignore",
            description: "Classes that should be completely ignored.");
        ignoreOpt.AddAlias("-i");
        

        var outputFileOption = new Option<FileInfo?>(
            name: "--output",
            description: "Optional output file");
        outputFileOption.AddAlias("-o");

        var rootCommand = new RootCommand("PlantUml Call Grapher");
        rootCommand.AddArgument(fileArg);
        rootCommand.AddArgument(namespaceArg);
        rootCommand.AddArgument(classArg);
        rootCommand.AddArgument(methodArg);
        rootCommand.AddOption(outputFileOption);
        rootCommand.AddOption(leafOpt);
        rootCommand.AddOption(ignoreOpt);
        
        rootCommand.SetHandler(async (file, ns, cs, mt, outputFile, leafs, ignores) =>
        {
            var writer = Console.Out;
            try
            {
                if (outputFile != null)
                    writer = new StreamWriter(outputFile.FullName);

                var processor = new Processor(writer, file.FullName, leafs, ignores);
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
            
        }, fileArg, namespaceArg, classArg, methodArg, outputFileOption, leafOpt, ignoreOpt);

        return rootCommand;
    }
}