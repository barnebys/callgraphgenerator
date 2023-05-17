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
    private Solution? _solution = null;
    private List<Project> _projects = new List<Project>();
    private readonly TextWriter _writer;
    private readonly Dictionary<SyntaxTree, Compilation> _compilations = new Dictionary<SyntaxTree, Compilation>();
    private readonly ISet<string> _leafs = new HashSet<string>();
    private readonly ISet<string> _ignores = new HashSet<string>();

    public Processor(TextWriter writer)
    {
        MSBuildLocator.RegisterDefaults();
        _workspace = MSBuildWorkspace.Create();
        _writer = writer;
        _ignores.Add("QueryFilterBuilder");
        _ignores.Add("Clock");
        _ignores.Add("SettingService");
        _ignores.Add("IAuthorizedUserService");
        _ignores.Add("SystemSettingsService");
        _ignores.Add("StreamExtensions");
        _ignores.Add("EnumerableExtensions");
        _ignores.Add("IOrderContext");
        _ignores.Add("InvoiceDataService");
        _ignores.Add("KeyValueSettingService");
        _ignores.Add("MarginVatService");
        _ignores.Add("InvoiceGeneratorFactory");
        _ignores.Add("WinnerTrackingService");
        _ignores.Add("CompanyService");
        _ignores.Add("IInvoiceGenerator");
        _ignores.Add("CustomerService");
        _ignores.Add("InventoryDocumentService");
        _ignores.Add("PaymentContext");
        _ignores.Add("GenericRepository");
        
        _leafs.Add("EAccountingInvoicingService");
        _leafs.Add("InvoiceSettingService");
        _leafs.Add("InventoryDocumentService");
        _leafs.Add("SmailService");
        _leafs.Add("EUTaxCalculator");
        _leafs.Add("AuctionService");
        _leafs.Add("AuctionPaymentService");
        _leafs.Add("InventoryItemService");
        
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
            _solution = solution;
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
        Console.WriteLine("Compiling...");
        foreach (var project in _projects)
        {
            Console.WriteLine($"Compiling project {project.Name}...");
            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
                throw new Exception($"Unable to fetch Compilation for Project {project.Name}");
            
            foreach (var syntaxTree in compilation.SyntaxTrees)
                _compilations.Add(syntaxTree, compilation);
        }


        MethodDeclarationSyntax? method = null;
        foreach (var project in _projects)
        {
            Console.WriteLine($"Fetching entry point...");
            var compilation = await project.GetCompilationAsync();
            method = GetEntry(compilation, nameSpace, className, methodName);
            if (method != null)
                break;
        }

        if (method == null)
        {
            Console.WriteLine("No entry found...");
            return;
        }

        Console.WriteLine($"Walking graph...");
        _writer.WriteLine("@startuml"); 
        await WalkCallGraph(method, "entry");
        _writer.WriteLine("@enduml");
    }

    private async Task WalkCallGraph(MethodDeclarationSyntax method, string caller)
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
        
        _writer.WriteLine($"{caller} -> {className}++: {methodName}");
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
                await WalkCallGraph(methodSyntax, className);
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