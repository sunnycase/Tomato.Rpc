using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tomato.Rpc.Proxy.DesignTime
{
    public class GenerateRpcPeerProxy : Microsoft.Build.Utilities.Task, ICancelableTask, ITask
    {
        public ITaskItem[] Compile { get; set; }
        public ITaskItem[] ReferencePath { get; set; }

        private readonly List<ITaskItem> _generatedCodeFiles = new List<ITaskItem>();
        [Output]
        public ITaskItem[] GeneratedCodeFiles => _generatedCodeFiles.ToArray();

        public string TargetName { get; set; }

        private CancellationTokenSource _cts;

        public override bool Execute()
        {
            AppDomainSetup appDomainSetup = new AppDomainSetup();
            appDomainSetup.ApplicationBase = Path.GetDirectoryName(base.GetType().Assembly.Location);
            appDomainSetup.DisallowBindingRedirects = true;
            AppDomain appDomain = AppDomain.CreateDomain("codegen", AppDomain.CurrentDomain.Evidence, appDomainSetup);
            bool result;
            try
            {
                var cts = new CancellationTokenSource();
                _cts = cts;
                var task = ExecuteAsync(cts.Token);
                task.Wait(cts.Token);
                return task.Result;
            }
            catch (OperationCanceledException)
            {
                base.Log.LogMessage(MessageImportance.High, "Canceled.");
                result = false;
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
            return result;
        }

        private async Task<bool> ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var compilation = CompileAll();

            ct.ThrowIfCancellationRequested();

            var generator = new RpcPeerProxyGenerator(compilation) { Log = Log };
            for (int i = 0; i < compilation.SyntaxTrees.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                await generator.Generate(compilation.SyntaxTrees[i], ct);

                ct.ThrowIfCancellationRequested();
                if (generator.Output.Members.Any())
                {
                    string inputFileName = Compile[i].ItemSpec;
                    string outputFileName = Path.ChangeExtension(inputFileName, ".RpcProxy.cs");
                    using (var destination = new StreamWriter(new FileStream(outputFileName, FileMode.Create), Encoding.UTF8))
                    {
                        await destination.WriteAsync(generator.Output.NormalizeWhitespace().ToFullString());
                        if (!Compile.Any(o => o.ItemSpec.Contains(outputFileName)))
                            _generatedCodeFiles.Add(new TaskItem(outputFileName));
                    }
                }
            }
            return true;
        }

        private CSharpCompilation CompileAll()
        {
            var syntaxTrees = Compile.Select(o => File.ReadAllText(o.GetMetadata("FullPath"))).Select(o => CSharpSyntaxTree.ParseText(o));
            var references = ReferencePath.Select(o => o.GetMetadata("FullPath")).Select(o => MetadataReference.CreateFromFile(o));

            return CSharpCompilation.Create("GenerateRpcPeerProxy.Metadata", syntaxTrees, references);
        }

        public void Cancel()
        {
            _cts.Cancel();
        }
    }
}
