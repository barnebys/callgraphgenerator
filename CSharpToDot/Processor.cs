using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace CSharpToDot;

public class Processor : IDisposable
{
    private readonly MSBuildWorkspace _workspace;
    private List<Project> _projects = new List<Project>();
    private readonly TextWriter _writer;

    public Processor(TextWriter writer)
    {
        MSBuildLocator.RegisterDefaults();
        _workspace = MSBuildWorkspace.Create();
        _writer = writer;

    }
    
    public void Dispose()
    {
        _workspace.Dispose();
    }

    public async Task OpenAsync(string filepath)
    {
        if (filepath.EndsWith("sln"))
        {
            var solution = await _workspace.OpenSolutionAsync(filepath);
            _projects.AddRange(solution.Projects);
        }
        else if (filepath.EndsWith("csproj"))
        {
            var project = await _workspace.OpenProjectAsync(filepath);
            _projects.Add(project);
        }
        else
        {
            throw new Exception("Unknown file type");
        }
    }

    public async Task RunAsync(string nameSpace, string className, string methodName)
    {
        foreach (var project in _projects)
        {
            Console.WriteLine($"Compiling project {project.Name}...");
            var compilation = await project.GetCompilationAsync();
            
            if (compilation == null)
                throw new Exception($"Unable to fetch Compilation for Project {project.Name}");
            Console.WriteLine($"Fetching entry point...");
            var method = GetEntry(compilation, nameSpace, className, methodName);
            Console.WriteLine($"Walking graph...");
            _writer.WriteLine("@plantuml");
            if (method != null)
                WalkCallGraph(compilation, method, "entry");
            _writer.WriteLine("@enduml");
        }
        
    }

    private void WalkCallGraph(Compilation compilation, MethodDeclarationSyntax method, string caller)
    {
        var model = compilation.GetSemanticModel(method.SyntaxTree);
        var classNode = FindClassParent(method);

        var className = classNode?.Identifier.ValueText ?? "root";
        var methodName = method.Identifier.ValueText;

        _writer.WriteLine($"{caller} -> {className}++: {methodName}");
        var bodyInvocations = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();
        var bodySymbols = bodyInvocations
            .Select(i => model.GetSymbolInfo(i).Symbol);

        foreach (var bodySymbol in bodySymbols)
        {
            var declaringSyntax = bodySymbol?.DeclaringSyntaxReferences;
            if (!declaringSyntax.HasValue || declaringSyntax.Value.Length == 0)
                continue;
            if (declaringSyntax.Value.Length != 1)
                throw new Exception($"Unable to find single source for symbol {bodySymbol?.Name}");
            var bodySyntax = declaringSyntax.Value.Single(); 

            if (bodySyntax.GetSyntax() is MethodDeclarationSyntax methodSyntax)
            {
                WalkCallGraph(compilation, methodSyntax, className);
            }
        }
        _writer.WriteLine("return");
    }
    
    private static ClassDeclarationSyntax? FindClassParent(MethodDeclarationSyntax method)
    {
        var parentNode = method.Parent;
        var classNode = parentNode as ClassDeclarationSyntax;
        while (parentNode != null && classNode == null)
        {
            parentNode = parentNode.Parent;
            classNode = parentNode as ClassDeclarationSyntax;
        }

        return classNode;
    }
    private MethodDeclarationSyntax? GetEntry(Compilation compilation, string nameSpace, string className, string methodName)
    {
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