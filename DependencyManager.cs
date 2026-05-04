using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DependencyManagement {

    public abstract class DependencyManager : AssetPostprocessor {

        protected static readonly List<AsmdefDependencies> AsmdefsDependencies = new();

        #region MenuItem Tools/

        [MenuItem("Tools/Dependency Managers/Force Reference Registered Dependencies", false, 0)]
        public static void ForceReferenceRegisteredDependencies() {
            foreach (AsmdefDependencies dependencies in AsmdefsDependencies)
                dependencies.ReferenceDependencies();

            AssetDatabase.Refresh();
        }

        // [MenuItem("Tools/Dependency Managers/(Global) Clear Referenced Dependencies", false, 1)]
        // private static void GlobalClearReferences() {
        //     Debug.Log("(Global) Clearing referenced dependencies");
        //
        //     foreach (string asmdefPath in EnumerateAllAsmdefsPaths())
        //         if (AsmdefsDependencies.Any(d => d.asmdef == Path.GetFileName(asmdefPath)))
        //             ClearReferences(asmdefPath);
        //
        //     AssetDatabase.Refresh();
        // }

        #endregion

        #region MenuItem Assets/

        [MenuItem("Assets/Dependency Management/Force Reference Dependencies", false, 0)]
        private static void ForceReferenceDependencies() {
            ClearReferencedDependencies();
            AsmdefsDependencies.First(a => a.asmdef == $"{Selection.activeObject.name}.asmdef").ReferenceDependencies();
            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/Dependency Management/Clear Referenced Dependencies", false, 1)]
        private static void ClearReferencedDependencies() {
            ClearReferences(AssetDatabase.GetAssetPath(Selection.activeObject));
            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/Dependency Management/Create Dependency Manager", false, 2)]
        private static void CreateDependencyManager() {
            Object selectedAsmdef = Selection.activeObject;

            // @formatter:off
            string script =
                "using DependencyManagement;\n" +
                "using UnityEditor;\n" +
                "\n" +
                "namespace YourNamespace.Optional {\n" +
                "\n" +
                "    [InitializeOnLoad]\n" +
                "    public class DependencyManager : DependencyManagement.DependencyManager {\n" +
                "\n" +
                "        static DependencyManager() {\n" +
                "            AsmdefsDependencies.Add(new AsmdefDependencies(\"" + selectedAsmdef.name + ".asmdef\", \"" + selectedAsmdef.name.ToUpper() + "_RUNTIME_\")\n" +
                "                .SetHardDependencies(\n" +
                "                    new(\"DEFINE\",\n" +
                "                        \"AsmdefName\"))\n" +
                "                .SetSoftDependencies(\n" +
                "	                 new(\"DEFINE\",\n" +
                "                        \"AsmdefName\")));\n" +
                "        }\n" +
                "\n" +
                "    }\n" +
                "\n" +
                "}";
            // @formatter:on

            string selectedAsmdefDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(selectedAsmdef));
            string root              = Path.Combine(selectedAsmdefDir!, "DependencyManager");
            Directory.CreateDirectory(root);

            string asmrefPath = Path.Combine(root, "dependencyManager.asmref");
            AsmrefData.CreateAtPath(asmrefPath, "DependencyManager");
            string managerPath = Path.Combine(root, "DependencyManager.cs");
            File.WriteAllText(managerPath, script, Encoding.UTF8);

            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/Dependency Management/Force Reference Dependencies", true)]
        private static bool SelectedRegisteredAsmdefValidator() {
            return AsmdefsDependencies.Any(a => a.asmdef == $"{Selection.activeObject.name}.asmdef");
        }

        [MenuItem("Assets/Dependency Management/Clear Referenced Dependencies", true)]
        [MenuItem("Assets/Dependency Management/Create Dependency Manager", true)]
        private static bool SelectedAsmdefValidator() {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return Path.GetExtension(path) == ".asmdef";
        }

        #endregion

        private static void ClearReferences(string filePath) {
#if DEBUG_DEPENDENCY_MANAGEMENT
            Debug.Log($"Clearing referenced dependencies for {filePath}");
#endif
            AsmdefData asmdef = new(filePath);
            asmdef.ClearReferencesAndDefines();
            asmdef.WriteToFile();
        }

        private static IEnumerable<string> EnumerateAllAsmdefsPaths() {
            // Assets
            foreach (string filePath in Directory.EnumerateFiles(Application.dataPath, "*.asmdef", SearchOption.AllDirectories))
                yield return filePath;

            // Packages
            foreach (string filePath in Directory.EnumerateFiles(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Packages"), "*.asmdef", SearchOption.AllDirectories))
                yield return filePath;
        }

    }

}