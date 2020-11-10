using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Emit;
using System.IO;
using System.Reflection;

namespace CompileTimeMethodExecutionGenerator.Generator
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        // The attribute that allows decorating methods with [CompileTimeExecutor] can be added to the compilation
        private const string attributeText = @"
using System;
namespace CompileTimeMethodExecutionGenerator
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    sealed class CompileTimeExecutorAttribute : Attribute
    {
        public CompileTimeExecutorAttribute()
        {
        }
    }
}";

        public void Execute(GeneratorExecutionContext context)
        {
            // Always add the attribute itself to the compilation
            context.AddSource("CompileTimeExecutorAttribute", SourceText.From(attributeText, Encoding.UTF8));

            // retreive the populated receiver 
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;

            // we're going to create a new compilation that contains the attribute.
            // TODO: we should allow source generators to provide source during initialize, so that this step isn't required.
            CSharpParseOptions options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(attributeText, Encoding.UTF8), options));

            // get the newly bound attribute, and INotifyPropertyChanged
            INamedTypeSymbol attributeSymbol = compilation.GetTypeByMetadataName("CompileTimeMethodExecutionGenerator.CompileTimeExecutorAttribute");

            foreach (MethodDeclarationSyntax method in receiver.CandidateMethods)
            {
                SemanticModel model = compilation.GetSemanticModel(method.SyntaxTree);

                var methodSymbol = model.GetDeclaredSymbol(method) as IMethodSymbol;

                foreach (AttributeSyntax attribute in method.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Where(at => string.Equals("CompileTimeExecutor", at.Name.ToString())))
                {
                    string calculatedResult = CalculateResult(method, methodSymbol);

                    context.AddSource($"{methodSymbol.ContainingNamespace.ToDisplayString()}_{methodSymbol.ContainingType.Name}_{method.Identifier.Text}.gen.cs",
                        SourceText.From($@"namespace {methodSymbol.ContainingNamespace.ToDisplayString()}
{{
    public partial class {methodSymbol.ContainingType.Name}
    {{
        public string {method.Identifier.Text}CompileTime()
        {{
            return ""{calculatedResult}"";
        }}
    }}
}}", Encoding.UTF8));
                }
            }
        }

        private static string CalculateResult(MethodDeclarationSyntax method, IMethodSymbol methodSymbol)
        {
            string assemblyName = Path.GetRandomFileName();
            MetadataReference[] references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
            };

            CSharpParseOptions options = method.SyntaxTree.Options as CSharpParseOptions;

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { CSharpSyntaxTree.ParseText(
                    SourceText.From(
                        $"public class C {{ public {method.ReturnType.ToString()} M() {method.Body.ToFullString()} }}", Encoding.UTF8),
                    options) },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            try
            {
                using (var ms = new MemoryStream())
                {
                    EmitResult result = compilation.Emit(ms);

                    if (!result.Success)
                    {
                        IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic => 
                            diagnostic.IsWarningAsError || 
                            diagnostic.Severity == DiagnosticSeverity.Error);

                        throw new Exception(string.Join("\r\n", failures.Select(f => $"{f.Id} {f.GetMessage()}")));
                    }
                    else
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        Assembly assembly = Assembly.Load(ms.ToArray());

                        Type type = assembly.GetType("C");
                        object obj = Activator.CreateInstance(type);
                        var res = type.InvokeMember("M",
                            BindingFlags.Default | BindingFlags.InvokeMethod,
                            null,
                            obj,
                            new object[0])?.ToString();
                        
                        return res;
                    }
                }
            }
            catch(Exception ex)
            {
                return $"Exception when executing method for injecting into compilation {ex.Message}";
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        /// <summary>
        /// Created on demand before each generation pass
        /// </summary>
        class SyntaxReceiver : ISyntaxReceiver
        {
            public List<MethodDeclarationSyntax> CandidateMethods { get; } = new List<MethodDeclarationSyntax>();

            /// <summary>
            /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
            /// </summary>
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // any method with at least one attribute is a candidate
                if (syntaxNode is MethodDeclarationSyntax methodDeclarationSyntax
                    && methodDeclarationSyntax.AttributeLists.Count > 0)
                {
                    CandidateMethods.Add(methodDeclarationSyntax);
                }
            }
        }
    }
}