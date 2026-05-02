using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DependencyManagement {

    public abstract class DependencyManager : AssetPostprocessor {

        protected static readonly List<AsmdefDependencies> AsmdefsDependencies = new();

        public static IEnumerable<string> EnumerateAllAsmdefsPaths() {
            // Assets
            foreach (string filePath in Directory.EnumerateFiles(Application.dataPath, "*.asmdef", SearchOption.AllDirectories))
                yield return filePath;

            // Packages
            foreach (string filePath in Directory.EnumerateFiles(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Packages"), "*.asmdef", SearchOption.AllDirectories))
                yield return filePath;
        }

        [MenuItem("Tools/Atomiz/Dependency Manager/Force Reference Dependencies")]
        public static void ReferenceDependencies() {
            foreach (AsmdefDependencies dependencies in AsmdefsDependencies)
                dependencies.ReferenceDependencies();

            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Atomiz/Dependency Manager/Clear Referenced Dependencies")]
        private static void ClearReferences() {
            Debug.Log("Resetting registered assemblies");

            foreach (string asmdefPath in EnumerateAllAsmdefsPaths())
                if (AsmdefsDependencies.Any(d => d.asmdef == Path.GetFileName(asmdefPath)))
                    reset(asmdefPath);

            AssetDatabase.Refresh();
            return;

            void reset(string filePath) {
                Debug.Log($"Resetting {filePath}");
                AsmdefData asmdef = new(filePath);
                asmdef.ClearReferencesAndDefines();
                asmdef.WriteToFile();
            }
        }

    }

}