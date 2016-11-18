using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Build.Utilities;
using Task = System.Threading.Tasks.Task;
using Microsoft.CodeAnalysis;

namespace Tomato.Rpc.Proxy.DesignTime
{
    class RpcPeerProxyGenerator
    {
        public TaskLoggingHelper Log { get; set; }
        public CSharpCompilation Compilation { get; }
        public CompilationUnitSyntax Output { get; set; }

        public RpcPeerProxyGenerator(CSharpCompilation compilation)
        {
            Compilation = compilation;
        }

        public async Task Generate(SyntaxTree syntaxTree, CancellationToken ct)
        {
            var semanticModel = Compilation.GetSemanticModel(syntaxTree);
            var rpcInterfaces = FindRpcPeerInterfaces(semanticModel, syntaxTree);
            var srcCompilationUnit = (CompilationUnitSyntax)syntaxTree.GetRoot();
            Output = SyntaxFactory.CompilationUnit().WithUsings(srcCompilationUnit.Usings)
                .WithMembers(new SyntaxList<MemberDeclarationSyntax>().AddRange(rpcInterfaces.Select(o => GenerateInterfaceProxy(o, semanticModel, syntaxTree))));
        }

        private MemberDeclarationSyntax GenerateInterfaceProxy(InterfaceDeclarationSyntax item, SemanticModel semanticModel, SyntaxTree syntaxTree)
        {
            var packetsVisitor = new RpcPeerInterfaceProxyPacketsGenerator(semanticModel);
            item.Accept(packetsVisitor);

            return SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(((NamespaceDeclarationSyntax)item.Parent).Name.ToFullString() + ".Rpc"))
                .WithMembers(new SyntaxList<MemberDeclarationSyntax>()
                .Add(CreateCallingProxyClass(item, semanticModel))
                .Add(CreateCalledProxyClass(item, semanticModel))
                .Add(
                    SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("Packets"))
                    .WithMembers(new SyntaxList<MemberDeclarationSyntax>().AddRange(packetsVisitor.Classes))
                    ));
        }

        private ClassDeclarationSyntax CreateCallingProxyClass(InterfaceDeclarationSyntax item, SemanticModel semanticModel)
        {
            var callingVisitor = new RpcPeerInterfaceCallingProxyGenerator(semanticModel);
            item.Accept(callingVisitor);

            var identifier = $"{item.Identifier.Text.TrimStart('I')}CallingProxy";
            var dispatherType = SyntaxFactory.ParseTypeName("Tomato.Rpc.Core.ICallingProxyDispatcher");

            return SyntaxFactory.ClassDeclaration(identifier)
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                    .WithBaseList(SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(new BaseTypeSyntax[] {
                        SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(item.Identifier.Text))
                    })))
                    .WithMembers(new SyntaxList<MemberDeclarationSyntax>().Add(
                        SyntaxFactory.PropertyDeclaration(dispatherType, "Dispatcher")
                        .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(new[]
                        {
                            SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                        })))
                        ).Add(
                        SyntaxFactory.ConstructorDeclaration(identifier)
                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                        .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(new[] {
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("dispatcher")).WithType(dispatherType)})))
                        .WithBody(SyntaxFactory.Block(
                            SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                SyntaxFactory.IdentifierName("Dispatcher"), SyntaxFactory.IdentifierName("dispatcher"))
                            )))
                        ).AddRange(callingVisitor.Members));
        }

        private ClassDeclarationSyntax CreateCalledProxyClass(InterfaceDeclarationSyntax item, SemanticModel semanticModel)
        {
            var calledVisitor = new RpcPeerInterfaceCalledProxyGenerator(semanticModel);
            item.Accept(calledVisitor);

            var identifier = $"{item.Identifier.Text.TrimStart('I')}CalledProxy";
            var serviceType = SyntaxFactory.ParseTypeName(item.Identifier.Text);

            return SyntaxFactory.ClassDeclaration(identifier)
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                    .WithBaseList(SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(calledVisitor.Bases)))
                    .WithMembers(new SyntaxList<MemberDeclarationSyntax>()
                        .Add(
                        SyntaxFactory.PropertyDeclaration(serviceType, "Service")
                        .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(new[]
                        {
                            SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                        })))
                        ).Add(
                        SyntaxFactory.ConstructorDeclaration(identifier)
                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                        .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(new[] {
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("service")).WithType(serviceType)
                        })))
                        .WithBody(SyntaxFactory.Block(
                            SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                SyntaxFactory.IdentifierName("Service"), SyntaxFactory.IdentifierName("service"))
                            )
                        ))).AddRange(calledVisitor.Members));
        }

        private IReadOnlyCollection<InterfaceDeclarationSyntax> FindRpcPeerInterfaces(SemanticModel semanticModel, SyntaxTree syntaxTree)
        {
            var visitor = new RpcPeerInterfaceVisitor(semanticModel) { Log = Log };
            visitor.Visit(syntaxTree.GetRoot());
            return visitor.Interfaces;
        }

        private static string GetPacketNameFromMethod(SemanticModel semanticModel, MethodDeclarationSyntax method)
        {
            return method.Identifier.Text + "__" +
            string.Join("__", method.ParameterList.Parameters.Select(o =>
           {
               var symbol = semanticModel.GetSymbolInfo(o.Type).Symbol;
               return Uri.EscapeDataString(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Replace('%', '_').Replace('.', '_');
           }));
        }

        class RpcPeerInterfaceVisitor : CSharpSyntaxWalker
        {
            public TaskLoggingHelper Log { get; set; }

            private readonly List<InterfaceDeclarationSyntax> _interfaces = new List<InterfaceDeclarationSyntax>();
            public IReadOnlyCollection<InterfaceDeclarationSyntax> Interfaces => _interfaces;

            private readonly SemanticModel _semanticModel;

            public RpcPeerInterfaceVisitor(SemanticModel semanticModel)
            {
                _semanticModel = semanticModel;
            }

            public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
            {
                var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
                if (node.AttributeLists.SelectMany(o => o.Attributes).Any(o =>
                     _semanticModel.GetSymbolInfo(o.Name).Symbol.ContainingSymbol.GetFullMetadataName() == "Tomato.Rpc.RpcPeerAttribute"))
                    _interfaces.Add(node);
            }
        }

        class RpcPeerInterfaceCallingProxyGenerator : CSharpSyntaxWalker
        {
            private readonly List<MemberDeclarationSyntax> _members = new List<MemberDeclarationSyntax>();
            public IReadOnlyCollection<MemberDeclarationSyntax> Members => _members;

            private readonly SemanticModel _semanticModel;

            public RpcPeerInterfaceCallingProxyGenerator(SemanticModel semanticModel)
            {
                _semanticModel = semanticModel;
            }

            public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                var member = SyntaxFactory.MethodDeclaration(node.ReturnType, node.Identifier)
                    .WithParameterList(node.ParameterList);
                var constructCallExp = SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName("Packets." + GetPacketNameFromMethod(_semanticModel, node)))
                    .WithInitializer(SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression,
                        SyntaxFactory.SeparatedList<ExpressionSyntax>(node.ParameterList.Parameters.Select((o, i) =>
                            SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                SyntaxFactory.IdentifierName($"Arg{i}"), SyntaxFactory.IdentifierName(o.Identifier))))));

                var returnName = _semanticModel.GetSymbolInfo(node.ReturnType).Symbol.GetFullMetadataName();
                if (returnName == "System.Void")
                    member = member
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithBody(SyntaxFactory.Block(
                        SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("Dispatcher"), SyntaxFactory.IdentifierName("DoCall")),
                                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[] { SyntaxFactory.Argument(constructCallExp) }))
                            ))));
                else if (returnName == "System.Threading.Tasks.Task")
                {
                    member = member
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.AsyncKeyword)))
                    .WithBody(SyntaxFactory.Block(
                        SyntaxFactory.ExpressionStatement(SyntaxFactory.AwaitExpression(
                            SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("Dispatcher"), SyntaxFactory.IdentifierName("DoCallAndWaitAnswer")),
                                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[] { SyntaxFactory.Argument(constructCallExp) }))
                            )))
                    ));
                }
                else
                {
                    var returnType = ((GenericNameSyntax)node.ReturnType).TypeArgumentList.Arguments.Single();
                    member = member
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.AsyncKeyword)))
                    .WithBody(SyntaxFactory.Block(
                        SyntaxFactory.ReturnStatement(SyntaxFactory.CastExpression(returnType, SyntaxFactory.AwaitExpression(
                            SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("Dispatcher"), SyntaxFactory.IdentifierName("DoCallAndWaitAnswer")),
                                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[] { SyntaxFactory.Argument(constructCallExp) }))
                            )))
                    )));
                }

                _members.Add(member);
            }
        }

        class RpcPeerInterfaceProxyPacketsGenerator : CSharpSyntaxWalker
        {
            private readonly List<ClassDeclarationSyntax> _classes = new List<ClassDeclarationSyntax>();
            public IReadOnlyCollection<MemberDeclarationSyntax> Classes => _classes;

            private readonly SemanticModel _semanticModel;

            public RpcPeerInterfaceProxyPacketsGenerator(SemanticModel semanticModel)
            {
                _semanticModel = semanticModel;
            }

            public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                var fields = node.ParameterList.Parameters.Select((o, i) =>
                    SyntaxFactory.FieldDeclaration(SyntaxFactory.List<AttributeListSyntax>(), SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)),
                    SyntaxFactory.VariableDeclaration(o.Type, SyntaxFactory.SeparatedList(new[] { SyntaxFactory.VariableDeclarator($"Arg{i}") })), SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
                _classes.Add(SyntaxFactory.ClassDeclaration(GetPacketNameFromMethod(_semanticModel, node))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.SealedKeyword)))
                    .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(fields)));
            }
        }

        class RpcPeerInterfaceCalledProxyGenerator : CSharpSyntaxWalker
        {
            private readonly List<MemberDeclarationSyntax> _members = new List<MemberDeclarationSyntax>();
            public IReadOnlyCollection<MemberDeclarationSyntax> Members => _members;
            private readonly List<BaseTypeSyntax> _bases = new List<BaseTypeSyntax>();
            public IReadOnlyCollection<BaseTypeSyntax> Bases => _bases;

            private readonly SemanticModel _semanticModel;

            public RpcPeerInterfaceCalledProxyGenerator(SemanticModel semanticModel)
            {
                _semanticModel = semanticModel;
            }

            public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                var packetName = $"Packets.{GetPacketNameFromMethod(_semanticModel, node)}";
                var returnName = _semanticModel.GetSymbolInfo(node.ReturnType).Symbol.GetFullMetadataName();

                if (returnName == "System.Void")
                    _bases.Add(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(
                        $"Tomato.Rpc.Core.IHandleRpcPacket<{packetName}> ")));
                else
                    _bases.Add(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(
                        $"Tomato.Rpc.Core.IHandleRpcPacket<{packetName}, {node.ReturnType.ToFullString()}> ")));

                var invokeExp = SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("Service"), SyntaxFactory.IdentifierName(node.Identifier.Text)),
                            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(node.ParameterList.Parameters.Select((o, i) =>
                                SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("args"), SyntaxFactory.IdentifierName($"Arg{i}")))))));

                _members.Add(SyntaxFactory.MethodDeclaration(node.ReturnType, "Handle")
                    .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(new[] {
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("args")).WithType(SyntaxFactory.ParseTypeName(packetName))
                    })))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithBody(SyntaxFactory.Block(returnName == "System.Void" ? 
                        (StatementSyntax)SyntaxFactory.ExpressionStatement(invokeExp) : SyntaxFactory.ReturnStatement(invokeExp))));
            }
        }
    }

    static class SymbolExtensions
    {
        public static string GetFullMetadataName(this ISymbol symbol)
        {
            ISymbol s = symbol;
            var sb = new StringBuilder(s.MetadataName);

            var last = s;
            s = s.ContainingSymbol;
            while (!IsRootNamespace(s))
            {
                if (s is ITypeSymbol && last is ITypeSymbol)
                {
                    sb.Insert(0, '+');
                }
                else
                {
                    sb.Insert(0, '.');
                }
                sb.Insert(0, s.MetadataName);
                s = s.ContainingSymbol;
            }

            return sb.ToString();
        }

        private static bool IsRootNamespace(ISymbol s)
        {
            return s is INamespaceSymbol && ((INamespaceSymbol)s).IsGlobalNamespace;
        }
    }
}
