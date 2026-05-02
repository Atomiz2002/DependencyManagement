using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DependencyManagement {

    public abstract class DependencyManager : AssetPostprocessor {

        protected static readonly List<AsmdefDependencies> AsmdefsDependencies = new();

        [MenuItem("Tools/Atomiz/Dependency Manager/Force Reference Dependencies")]
        public static void ReferenceDependencies() {
            foreach (AsmdefDependencies dependencies in AsmdefsDependencies)
                dependencies.ReferenceDependencies();

            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Atomiz/Dependency Manager/Clear Referenced Dependencies")]
        private static void ClearReferences() {
            Debug.Log("Resetting registered assemblies");
            // Assets
            foreach (string filePath in Directory.EnumerateFiles(Application.dataPath, "*.asmdef", SearchOption.AllDirectories))
                if (AsmdefsDependencies.Any(d => d.asmdef == Path.GetFileName(filePath)))
                    reset(filePath);

            // Packages
            foreach (string filePath in Directory.EnumerateFiles(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Packages"), "*.asmdef", SearchOption.AllDirectories)) {
                if (AsmdefsDependencies.Any(d => d.asmdef == Path.GetFileName(filePath)))
                    reset(filePath);
            }

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