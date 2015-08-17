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

        ITypeSymbol _cancellationTokenSymbol;

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
                        rewrittenTrees.SelectMany(t => t.GetCompilationUnitRoot().Usings)
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
            _cancellationTokenSymbol = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");

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
                            clsGrp.Key.WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(
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
            var rewriter = new MethodInvocationRewriter(_log, semanticModel, _excludedTypes, _cancellationTokenSymbol);
            var outMethod = (MethodDeclarationSyntax)rewriter.Visit(inMethodSyntax);

            // Method signature
            outMethod = outMethod
                .WithIdentifier(SyntaxFactory.Identifier(outMethodName))
                .WithAttributeLists(new SyntaxList<AttributeListSyntax>())
                .WithModifiers(inMethodSyntax.Modifiers
                  .Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword))
                  //.Remove(SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
                  //.Remove(SyntaxFactory.Token(SyntaxKind.NewKeyword))
                )
                // Transform parameters adding cancellation token
                .WithParameterList(SyntaxFactory.ParameterList(inMethodSyntax.ParameterList.Parameters.Insert(0, SyntaxFactory.Parameter(
                        SyntaxFactory.List<AttributeListSyntax>(),
                        SyntaxFactory.TokenList(),
                        SyntaxFactory.ParseTypeName("CancellationToken"),
                        SyntaxFactory.Identifier("cancellationToken"),
                        null
                ))));

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
        readonly ITypeSymbol _cancellationTokenSymbol;
        readonly ParameterComparer _paramComparer;
        readonly ILogger _log;

        public MethodInvocationRewriter(ILogger log, SemanticModel model, HashSet<ITypeSymbol> excludeTypes,
                                        ITypeSymbol cancellationTokenSymbol)
        {
            _log = log;
            _model = model;
            _cancellationTokenSymbol = cancellationTokenSymbol;
            _excludeTypes = excludeTypes;
            _paramComparer = new ParameterComparer();
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var syncSymbol = (IMethodSymbol)_model.GetSymbolInfo(node).Symbol;
            if (syncSymbol == null)
                return node;

            int cancellationTokenPos;

            // Skip invocations of methods that don't have [RewriteAsync], or an Async
            // counterpart to them
            if (syncSymbol.GetAttributes().Any(a => a.AttributeClass.Name == "RewriteAsyncAttribute"))
            {
                // This is one of our methods, flagged for async rewriting.
                // All methods rewritten by us accept a cancellation token (for now), as the first argument.
                cancellationTokenPos = 0;
            }
            else
            {
                if (_excludeTypes.Contains(syncSymbol.ContainingType))
                    return node;

                var asyncCandidates = syncSymbol.ContainingType.GetMembers(syncSymbol.Name + "Async");

                // First attempt to find an async method accepting a cancellation token.
                // Assume it appears in last position (for now).
                var asyncWithCancellationToken = asyncCandidates
                    .Cast<IMethodSymbol>()
                    .FirstOrDefault(ms =>
                        ms.Parameters.Length == syncSymbol.Parameters.Length + 1 &&
                        ms.Parameters.Take(syncSymbol.Parameters.Length).SequenceEqual(syncSymbol.Parameters, _paramComparer) &&
                        ms.Parameters.Last().Type == _cancellationTokenSymbol
                    );
                if (asyncWithCancellationToken != null)
                {
                    cancellationTokenPos = asyncWithCancellationToken.Parameters.Length - 1;
                }
                else
                {
                    // No async overload that accepts a cancellation token.
                    // Make sure there's an async target with a matching parameter list, otherwise don't rewrite
                    if (asyncCandidates
                        .Cast<IMethodSymbol>()
                        .Any(ms =>
                            ms.Parameters.Length == syncSymbol.Parameters.Length &&
                            ms.Parameters.SequenceEqual(syncSymbol.Parameters)
                        )
                    )
                    {
                        cancellationTokenPos = -1;
                    }
                    else
                    {
                        return node;
                    }

                }
            }

            _log.Debug("    Found rewritable invocation: " + syncSymbol);

            var rewritten = RewriteExpression(node, cancellationTokenPos);
            if (!(node.Parent is StatementSyntax))
                rewritten = SyntaxFactory.ParenthesizedExpression(rewritten);
            return rewritten;
        }

        ExpressionSyntax RewriteExpression(InvocationExpressionSyntax node, int cancellationTokenPos)
        {
            InvocationExpressionSyntax rewrittenInvocation = null;

            if (node.Expression is IdentifierNameSyntax)
            {
                var identifierName = (IdentifierNameSyntax)node.Expression;
                rewrittenInvocation = node.WithExpression(identifierName.WithIdentifier(
                    SyntaxFactory.Identifier(identifierName.Identifier.Text + "Async")
                ));
            }
            else if (node.Expression is MemberAccessExpressionSyntax)
            {
                var memberAccessExp = (MemberAccessExpressionSyntax)node.Expression;
                var nestedInvocation = memberAccessExp.Expression as InvocationExpressionSyntax;
                if (nestedInvocation != null)
                    memberAccessExp = memberAccessExp.WithExpression((ExpressionSyntax)VisitInvocationExpression(nestedInvocation));

                rewrittenInvocation = node.WithExpression(memberAccessExp.WithName(
                    memberAccessExp.Name.WithIdentifier(
                        SyntaxFactory.Identifier(memberAccessExp.Name.Identifier.Text + "Async")
                    )
                ));
            }
            else if (node.Expression is GenericNameSyntax)
            {
                var genericNameExp = (GenericNameSyntax)node.Expression;
                rewrittenInvocation = node.WithExpression(
                    genericNameExp.WithIdentifier(SyntaxFactory.Identifier(genericNameExp.Identifier.Text + "Async"))
                );
            }
            else throw new NotSupportedException($"It seems there's an expression type ({node.Expression.GetType().Name}) not yet supported by the AsyncRewriter");

            if (cancellationTokenPos != -1)
            {
                var cancellationTokenArg = SyntaxFactory.Argument(SyntaxFactory.IdentifierName("cancellationToken"));

                if (cancellationTokenPos == rewrittenInvocation.ArgumentList.Arguments.Count)
                    rewrittenInvocation = rewrittenInvocation.WithArgumentList(
                        rewrittenInvocation.ArgumentList.AddArguments(cancellationTokenArg)
                    );
                else
                    rewrittenInvocation = rewrittenInvocation.WithArgumentList(SyntaxFactory.ArgumentList(
                        rewrittenInvocation.ArgumentList.Arguments.Insert(cancellationTokenPos, cancellationTokenArg)
                    ));
            }

            return SyntaxFactory.AwaitExpression(rewrittenInvocation);
        }

        class ParameterComparer : IEqualityComparer<IParameterSymbol>
        {
            public bool Equals(IParameterSymbol x, IParameterSymbol y)
            {
                return
                    x.Name.Equals(y.Name) &&
                    x.Type.Equals(y.Type);
            }

            public int GetHashCode(IParameterSymbol p)
            {
                return p.GetHashCode();
            }
        }
    }
}
