using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DependencyManagement {

    public abstract class DependencyManager : AssetPostprocessor {

        protected static readonly List<AsmdefDependencies> AsmdefsDependencies = new();

        [MenuItem("Tools/Atomiz/Dependency Manager/Reference Dependencies")]
        public static void ReferenceDependencies() {
            foreach (AsmdefDependencies dependencies in AsmdefsDependencies)
                dependencies.ReferenceDependencies();

            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Atomiz/Dependency Manager/Reset Soft Dependencies")]
        private static void Reset() {
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
                AsmdefData asmdef = new(filePath);
                asmdef.ClearReferencesAndDefines();
                asmdef.WriteToFile();
            }
        }

        [MenuItem("Tools/Atomiz/Dependency Manager/Distinctify Soft Dependencies Version Defines")]
        private static void Distinct() {
            foreach (string path in AssetDatabase.FindAssets("t:AssemblyDefinitionAsset")
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(p => AsmdefsDependencies.Any(d => d.asmdef == Path.GetFileNameWithoutExtension(p)))) {
                new AsmdefData(path).WriteToFile();
            }

            AssetDatabase.Refresh();
        }

    }

}