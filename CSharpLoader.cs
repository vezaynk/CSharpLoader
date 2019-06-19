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
    public class CSharpLoader
    {
        public Assembly Assembly { get; set; }
        public CSharpLoader(string filename, string sdk)
        {
            Console.WriteLine("Loading file...");
            string codeToCompile = File.ReadAllText(filename, Encoding.UTF8);

            Console.WriteLine("Parsing the code into the SyntaxTree...");
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(codeToCompile);

            string assemblyName = Path.GetRandomFileName();
            var references = Directory
                                .GetFileSystemEntries(sdk, "*.dll")
                                .Append(Assembly.GetExecutingAssembly().Location)
                                .Select(path => MetadataReference.CreateFromFile(path));

            Console.WriteLine("Compiling...");
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    Console.WriteLine("Compilation failed!");
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures)
                    {
                        Console.Error.WriteLine("\t{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                    }
                    throw new InvalidProgramException("Invalid Program");
                }
                else
                {
                    Console.WriteLine("Compilation successful! Types can now be loaded from the assembly!");
                    ms.Seek(0, SeekOrigin.Begin);

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
