using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Text;

namespace CSharpLoader
{
    public class InMemoryCompiler
    {
        public Assembly Assembly { get; set; }
        public InMemoryCompiler(IEnumerable<string> filenames, string sdk)
        {
            IEnumerable<SyntaxTree> syntaxTrees = filenames
                                                    // Read in code to compile
                                                    .Select(filename => File.ReadAllText(filename, Encoding.UTF8))
                                                    // Generate syntax tree (Roslyn, see: https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/syntax-analysis)
                                                    .Select(body => CSharpSyntaxTree.ParseText(body));
            // Generate a random assembly name to avoid collisions
            string assemblyName = Path.GetRandomFileName();

            // The C# program probably needs references (ie. System.IO)
            // The compiled program may want to utilise the compiling program's namespaces
            //  including all the references used by the original program makes sense
            var references = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.Location)
                                // Sometimes those are not enough, in which case we should load the entire SDK just in case.
                                .Union(sdk != null ? Directory.GetFileSystemEntries(sdk, "*.dll") : Enumerable.Empty<string>())
                                // Create references to all the DLLs
                                .Select(path => MetadataReference.CreateFromFile(path));
            // TODO: Allow including multiple directories or specific files

            // Run compilation
            CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            // Include syntax tree. TODO: Allow for multiple programs
            syntaxTrees: syntaxTrees,
            // Include all references
            references: references,
            // Generate DLL (Static linking would result if a very large file, including the entire SDK)
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using (var ms = new MemoryStream())
            {
                // Emit result into memory stream
                EmitResult result = compilation.Emit(ms);
                if (!result.Success)
                {
                    // Compilation failed. Display all errors.
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error);
                    foreach (Diagnostic diagnostic in failures)
                    {
                        Console.Error.WriteLine("\t{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                    }
                    throw new InvalidProgramException("Compilation failed. See errors above.");
                }
                else
                {
                    // Compilation succeeded
                    // Rewind MemoryStream to beginning (ms.Position = size of DDL in bytes)
                    ms.Position = 0;
                    // Load DDL into memory
                    Assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
                }
            }
        }
        public Type GetType(string name)
        {
            return Assembly.GetType(name);
        }
    }
}
