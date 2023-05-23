using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.FindSymbols;

namespace CallGraphGenerator;

public class Processor : IDisposable
{
    private readonly MSBuildWorkspace _workspace;
    private Solution? _solution;
    private List<Project> _projects = new();
    private readonly string _filename;
    private readonly TextWriter _writer;
    private readonly ISet<string> _leafs;
    private readonly ISet<string> _ignores;
    private readonly Dictionary<SyntaxTree, Compilation> _compilations = new();

    private readonly IList<string> _colors = new List<string>
    {
        "Pink",
        "DarkSeaGreen",
        "CornflowerBlue",
        "PeachPuff",
        "LightSeaGreen",
        "Lavender",
        "Salmon",
        "Teal",
        "Turquoise",
    };

    public Processor(TextWriter writer, string filename, IEnumerable<string> leafs, IEnumerable<string> ignores)
    {
        MSBuildLocator.RegisterDefaults();
        _workspace = MSBuildWorkspace.Create();
        _writer = writer;
        _filename = filename;
        _leafs = new HashSet<string>(leafs);
        _ignores = new HashSet<string>(ignores);
    }

    void IDisposable.Dispose()
    {
        _workspace.Dispose();
    }

    private async Task OpenAsync()
    {
        Console.WriteLine($"Open file {_filename}...");
        if (_filename.EndsWith("sln"))
        {
            var solution = await _workspace.OpenSolutionAsync(_filename);
            _solution = solution;
            _projects.AddRange(solution.Projects);
        }
        else if (_filename.EndsWith("csproj"))
        {
            var project = await _workspace.OpenProjectAsync(_filename);
            _projects.Add(project);
        }
        else
        {
            throw new Exception("Unknown file type");
        }
    }

    public async Task RunAsync(string nameSpace, string className, string methodName)
    {
        await OpenAsync();
        Console.WriteLine("Compiling...");
        MethodDeclarationSyntax? method = null;
        foreach (var project in _projects)
        {
            Console.WriteLine($"Compiling project {project.Name}...");
            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
                throw new Exception($"Unable to fetch Compilation for Project {project.Name}");
            
            foreach (var syntaxTree in compilation.SyntaxTrees)
                _compilations.Add(syntaxTree, compilation);
            
            if (method == null)
                method = GetEntry(compilation, nameSpace, className, methodName);
        }

        if (method == null)
        {
            Console.WriteLine("No entry found...");
            return;
        }

        Console.WriteLine($"Walking graph...");
        _writer.WriteLine("@startuml"); 
        _writer.WriteLine($"title {nameSpace}:{className}:{methodName}()");
        await WalkCallGraph(method, "entry", 0);
        _writer.WriteLine("@enduml");
    }

    private async Task WalkCallGraph(MethodDeclarationSyntax method, string caller, int depth)
    {
        if (_leafs.Contains(caller))
            return;
        if (caller.EndsWith("Repository"))
            return;
        var compilation = _compilations[method.SyntaxTree];
        var model = compilation.GetSemanticModel(method.SyntaxTree);
        var classNode = FindClassParent(method);

        var className = classNode?.Identifier.ValueText ?? "root";
        var methodName = method.Identifier.ValueText;
        
        if (_ignores.Contains(className))
            return;

        var colorIndex = depth % _colors.Count;
        var color = _colors[colorIndex];
        _writer.WriteLine($"{caller} -> {className} ++ #{color}: {methodName}");
        var bodyInvocations = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();
        var bodySymbols = bodyInvocations
            .Select(i => model.GetSymbolInfo(i).Symbol)
            .Where(s => s != null);

        foreach (var bodySymbol in bodySymbols)
        {
            var implementations = (await SymbolFinder.FindImplementationsAsync(bodySymbol, _solution)).ToList();
            IList<SyntaxReference> declaringSyntax = null;

            if (implementations.Any())
            {
                declaringSyntax = implementations.SelectMany(i => i.DeclaringSyntaxReferences).ToList();
            }
            
            if (declaringSyntax == null || declaringSyntax.Count != 1)
            {
                declaringSyntax = bodySymbol.DeclaringSyntaxReferences.ToList();
            }
            if (!declaringSyntax.Any())
                continue;
            if (declaringSyntax.Count != 1)
                throw new Exception($"Unable to find single source for symbol {bodySymbol?.Name}");
            var bodySyntax = declaringSyntax.Single(); 

            if (await bodySyntax.GetSyntaxAsync() is MethodDeclarationSyntax methodSyntax)
            {
                await WalkCallGraph(methodSyntax, className, depth + 1);
            }
        }
        _writer.WriteLine("return");
    }
    
    private static TypeDeclarationSyntax? FindClassParent(MethodDeclarationSyntax method)
    {
        var parentNode = method.Parent;
        var classNode = parentNode as ClassDeclarationSyntax;
        var interfaceNode = parentNode as InterfaceDeclarationSyntax;
        while (parentNode != null && classNode == null && interfaceNode == null)
        {
            parentNode = parentNode.Parent;
            classNode = parentNode as ClassDeclarationSyntax;
            interfaceNode = parentNode as InterfaceDeclarationSyntax;
        }

        if (classNode != null)
            return classNode;
        return interfaceNode;
    }
    private MethodDeclarationSyntax? GetEntry(Compilation compilation, string nameSpace, string className, string methodName)
    {
        var foo = compilation.SyntaxTrees
            .Select(t => t.GetRoot())
            .SelectMany(r =>
                r.DescendantNodes()
                    .OfType<BaseNamespaceDeclarationSyntax>());
        var namespaces = compilation.SyntaxTrees
            .Select(t => t.GetRoot())
            .SelectMany(r =>
                r.DescendantNodes()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .Where(n => n.Name.ToString() == nameSpace));
        var classes = namespaces
            .SelectMany(ns =>
                ns.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Where(c => c.Identifier.ValueText == className)); 
        var methods = classes
            .SelectMany(c =>
                c.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Identifier.ValueText == methodName));
        return methods.SingleOrDefault();
    }

}