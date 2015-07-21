using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AsyncRewriter
{
    // Map namespaces to classes to methods, for methods that are marked
    using NamespaceToClasses = Dictionary<NamespaceDeclarationSyntax, Dictionary<ClassDeclarationSyntax, HashSet<MethodInfo>>>;

    /// <summary>
    /// </summary>
    /// <remarks>
    /// http://stackoverflow.com/questions/2961753/how-to-hide-files-generated-by-custom-tool-in-visual-studio
    /// </remarks>
    public class Rewriter
    {
        /// <summary>
        /// Invocations of methods on these types never get rewritten to async
        /// </summary>
        HashSet<ITypeSymbol> _excludedTypes;

        /// <summary>
        /// Using directives required for async, not expected to be in the source (sync) files
        /// </summary>
        static readonly UsingDirectiveSyntax[] ExtraUsingDirectives = {
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Threading")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Threading.Tasks")),
        };

        /// <summary>
        /// Calls of methods on these types never get rewritten, because they aren't actually
        /// asynchronous. An additional user-determined list may also be passed in.
        /// </summary>
        static readonly string[] AlwaysExcludedTypes = {
            "System.IO.TextWriter",
            "System.IO.StringWriter",
            "System.IO.MemoryStream"
        };

        /// <summary>
        /// Contains the parsed contents of the AsyncRewriterHelpers.cs file (essentially
        /// <see cref="RewriteAsync"/> which needs to always be compiled in.
        /// </summary>
        readonly SyntaxTree _asyncHelpersSyntaxTree;

        readonly ILogger _log;

        public Rewriter(ILogger log=null)
        {
            _log = log ?? new ConsoleLoggingAdapter();
            // ReSharper disable once AssignNullToNotNullAttribute
            using (var reader = new StreamReader(typeof(Rewriter).Assembly.GetManifestResourceStream("AsyncRewriter.AsyncRewriterHelpers.cs")))
            {
                _asyncHelpersSyntaxTree = SyntaxFactory.ParseSyntaxTree(reader.ReadToEnd());
            }
        }

        public string RewriteAndMerge(string[] paths, string[] additionalAssemblyNames=null, string[] excludedTypes = null)
        {
            if (paths.All(p => Path.GetFileName(p) != "AsyncRewriterHelpers.cs"))
                throw new ArgumentException("AsyncRewriterHelpers.cs must be included in paths", nameof(paths));
            Contract.EndContractBlock();

            var syntaxTrees = paths.Select(p => SyntaxFactory.ParseSyntaxTree(File.ReadAllText(p))).ToArray();

            var compilation = CSharpCompilation.Create(
                "Temp",
                syntaxTrees,
                (additionalAssemblyNames?.Select(n => MetadataReference.CreateFromFile(Assembly.Load(n).Location)) ?? new PortableExecutableReference[0])
                    .Concat(new[] {
                        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                        MetadataReference.CreateFromFile(typeof(Stream).Assembly.Location)
                    }),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            return RewriteAndMerge(syntaxTrees, compilation, excludedTypes).ToString();
        }

        public SyntaxTree RewriteAndMerge(SyntaxTree[] syntaxTrees, CSharpCompilation compilation, string[] excludedTypes = null)
        {
            var rewrittenTrees = Rewrite(syntaxTrees, compilation, excludedTypes).ToArray();

            return SyntaxFactory.SyntaxTree(
                SyntaxFactory.CompilationUnit()
                    .WithUsings(SyntaxFactory.List(
                        rewrittenTrees
                            .SelectMany(t => t.GetCompilationUnitRoot().Usings)
                            .GroupBy(u => u.Name.ToString())
                            .Select(g => g.First())
                    ))
                    .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(
                        rewrittenTrees
                            .SelectMany(t => t.GetCompilationUnitRoot().Members)
                            .Cast<NamespaceDeclarationSyntax>()
                            .SelectMany(ns => ns.Members)
                            .Cast<ClassDeclarationSyntax>()
                            .GroupBy(cls => cls.FirstAncestorOrSelf<NamespaceDeclarationSyntax>().Name.ToString())
                            .Select(g => SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(g.Key))
                                .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(g))
                            )
                    ))
                    .WithEndOfFileToken(SyntaxFactory.Token(SyntaxKind.EndOfFileToken))
                    .NormalizeWhitespace()
            );
        }

        public IEnumerable<SyntaxTree> Rewrite(SyntaxTree[] syntaxTrees, CSharpCompilation compilation, string[] excludedTypes=null)
        {
            _excludedTypes = new HashSet<ITypeSymbol>();

            // Handle the user-provided exclude list
            if (excludedTypes != null)
            {
                var excludedTypeSymbols = excludedTypes.Select(compilation.GetTypeByMetadataName).ToList();
                var notFound = excludedTypeSymbols.IndexOf(null);
                if (notFound != -1)
                    throw new ArgumentException($"Type {excludedTypes[notFound]} not found in compilation", nameof(excludedTypes));
                _excludedTypes.UnionWith(excludedTypeSymbols);
            }

            // And the builtin exclude list
            _excludedTypes.UnionWith(
                AlwaysExcludedTypes
                    .Select(compilation.GetTypeByMetadataName)
                    .Where(sym => sym != null)
            );

            foreach (var syntaxTree in syntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree, true);
                if (semanticModel == null)
                    throw new ArgumentException("A provided syntax tree was compiled into the provided compilation");

                var usings = syntaxTree.GetCompilationUnitRoot().Usings;

                var asyncRewriterUsing = usings.SingleOrDefault(u => u.Name.ToString() == "AsyncRewriter");
                if (asyncRewriterUsing == null)
                    continue;   // No "using AsyncRewriter", skip this file

                usings = usings
                    // Remove the AsyncRewriter using directive
                    .Remove(asyncRewriterUsing)
                    // Add the extra using directives
                    .AddRange(ExtraUsingDirectives);

                // Add #pragma warning disable at the top of the file
                usings = usings.Replace(usings[0], usings[0].WithLeadingTrivia(SyntaxFactory.Trivia(SyntaxFactory.PragmaWarningDirectiveTrivia(SyntaxFactory.Token(SyntaxKind.DisableKeyword), true))));
                    
                var namespaces = SyntaxFactory.List<MemberDeclarationSyntax>(
                    syntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m => m.AttributeLists.SelectMany(al => al.Attributes).Any(a => a.Name.ToString() == "RewriteAsync"))
                    .GroupBy(m => m.FirstAncestorOrSelf<ClassDeclarationSyntax>())
                    .GroupBy(g => g.Key.FirstAncestorOrSelf<NamespaceDeclarationSyntax>())
                    .Select(nsGrp =>
                        SyntaxFactory.NamespaceDeclaration(nsGrp.Key.Name)
                        .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(nsGrp.Select(clsGrp =>
                            SyntaxFactory.ClassDeclaration(clsGrp.Key.Identifier)
                            .WithModifiers(clsGrp.Key.Modifiers)
                            .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(
                                clsGrp.Select(m => RewriteMethod(m, semanticModel))
                            ))
                        )))
                    )
                );

                yield return SyntaxFactory.SyntaxTree(
                    SyntaxFactory.CompilationUnit()
                        .WithUsings(SyntaxFactory.List(usings))
                        .WithMembers(namespaces)
                        .WithEndOfFileToken(SyntaxFactory.Token(SyntaxKind.EndOfFileToken))
                        .NormalizeWhitespace()
                );
            }
        }

        MethodDeclarationSyntax RewriteMethod(MethodDeclarationSyntax inMethodSyntax, SemanticModel semanticModel)
        {
            var inMethodSymbol = semanticModel.GetDeclaredSymbol(inMethodSyntax);

            //Log.LogMessage("Method {0}: {1}", inMethodInfo.Symbol.Name, inMethodInfo.Symbol.);

            var outMethodName = inMethodSyntax.Identifier.Text + "Async";

            _log.Debug("  Rewriting method {0} to {1}", inMethodSymbol.Name, outMethodName);

            // Visit all method invocations inside the method, rewrite them to async if needed
            var rewriter = new MethodInvocationRewriter(_log, semanticModel, _excludedTypes);
            var outMethod = (MethodDeclarationSyntax)rewriter.Visit(inMethodSyntax);

            // Method signature
            outMethod = outMethod
                .WithIdentifier(SyntaxFactory.Identifier(outMethodName))
                .WithAttributeLists(new SyntaxList<AttributeListSyntax>())
                .WithModifiers(inMethodSyntax.Modifiers
                  .Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword))
                  //.Remove(SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
                  //.Remove(SyntaxFactory.Token(SyntaxKind.NewKeyword))
                );

            // Transform return type adding Task<>
            var returnType = inMethodSyntax.ReturnType.ToString();
            outMethod = outMethod.WithReturnType(SyntaxFactory.ParseTypeName(
                returnType == "void" ? "Task" : $"Task<{returnType}>")
            );

            // Remove the override and new attributes. Seems like the clean .Remove above doesn't work...
            for (var i = 0; i < outMethod.Modifiers.Count;)
            {
                var text = outMethod.Modifiers[i].Text;
                if (text == "override" || text == "new") {
                    outMethod = outMethod.WithModifiers(outMethod.Modifiers.RemoveAt(i));
                    continue;
                }
                i++;
            }

            var attr = inMethodSymbol.GetAttributes().Single(a => a.AttributeClass.Name == "RewriteAsyncAttribute");

            if (attr.ConstructorArguments.Length > 0 && (bool) attr.ConstructorArguments[0].Value)
            {
                outMethod = outMethod.AddModifiers(SyntaxFactory.Token(SyntaxKind.OverrideKeyword));
            }

            return outMethod;
        }
    }

    internal class MethodInvocationRewriter : CSharpSyntaxRewriter
    {
        readonly SemanticModel _model;
        readonly HashSet<ITypeSymbol> _excludeTypes;
        readonly ILogger _log;

        public MethodInvocationRewriter(ILogger log, SemanticModel model, HashSet<ITypeSymbol> excludeTypes)
        {
            _log = log;
            _model = model;
            _excludeTypes = excludeTypes;
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var symbol = (IMethodSymbol)_model.GetSymbolInfo(node).Symbol;
            if (symbol == null)
                return node;

            // Skip invocations of methods that don't have [RewriteAsync], or an Async
            // counterpart to them
            if (!symbol.GetAttributes().Any(a => a.AttributeClass.Name == "RewriteAsyncAttribute") && (
                  _excludeTypes.Contains(symbol.ContainingType) ||
                  !symbol.ContainingType.GetMembers(symbol.Name + "Async").Any()
               ))
            {
                return node;
            }

            _log.Debug("    Found rewritable invocation: " + symbol);

            var rewritten = RewriteExpression(node);
            if (!(node.Parent is StatementSyntax))
                rewritten = SyntaxFactory.ParenthesizedExpression(rewritten);
            return rewritten;
        }

        ExpressionSyntax RewriteExpression(InvocationExpressionSyntax node)
        {
            var identifierName = node.Expression as IdentifierNameSyntax;
            if (identifierName != null)
            {
                return SyntaxFactory.AwaitExpression(
                    node.WithExpression(identifierName.WithIdentifier(
                        SyntaxFactory.Identifier(identifierName.Identifier.Text + "Async")
                    ))
                );
            }

            var memberAccessExp = node.Expression as MemberAccessExpressionSyntax;
            if (memberAccessExp != null)
            {
                var nestedInvocation = memberAccessExp.Expression as InvocationExpressionSyntax;
                if (nestedInvocation != null)
                    memberAccessExp = memberAccessExp.WithExpression((ExpressionSyntax)VisitInvocationExpression(nestedInvocation));

                return SyntaxFactory.AwaitExpression(
                    node.WithExpression(memberAccessExp.WithName(
                        memberAccessExp.Name.WithIdentifier(SyntaxFactory.Identifier(memberAccessExp.Name.Identifier.Text + "Async"))
                    ))
                );
            }

            var genericNameExp = node.Expression as GenericNameSyntax;
            if (genericNameExp != null)
            {
                return SyntaxFactory.AwaitExpression(
                    node.WithExpression(
                        genericNameExp.WithIdentifier(SyntaxFactory.Identifier(genericNameExp.Identifier.Text + "Async"))
                    )
                );
            }

            throw new NotSupportedException($"It seems there's an expression type ({node.Expression.GetType().Name}) not yet supported by the AsyncRewriter");
        }
    }
}
