using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DependencyManagement {

    [Serializable]
    public class AsmdefData {

        public string name;

        // General Options
        public bool   allowUnsafeCode;
        public bool   autoReferenced;
        public bool   noEngineReferences;
        public bool   overrideReferences;
        public string rootNamespace;

        /// ASMDEFs
        public List<string> references;

        /// DLLs
        public List<string> precompiledReferences;

        // Platforms
        public List<string> includePlatforms;
        public List<string> excludePlatforms;

        public List<string>        defineConstraints = new();
        public List<VersionDefine> versionDefines    = new();

        private string path;

        public AsmdefData(string asmdefPath) {
            asmdefPath = asmdefPath.Replace(".asmdef.json", ".asmdef");
            JsonUtility.FromJsonOverwrite(File.ReadAllText(asmdefPath, Encoding.UTF8), this);
            // overrideReferences = true; // apparently no speed boost by directly referencing .dlls
            path = asmdefPath;
        }

        public void WriteToFile(string asmdefPath = null) {
            DistinctDefines(); // todo remove
            File.WriteAllText(asmdefPath?.Replace(".asmdef.json", ".asmdef") ?? path, JsonUtility.ToJson(this, true), Encoding.UTF8);
        }

        public void DistinctDefines() {
            defineConstraints = defineConstraints.Distinct().ToList();
            HashSet<string> defines = new();
            versionDefines = versionDefines.Where(item => defines.Add(item.define)).ToList();
        }

        public void ClearReferencesAndDefines() {
            defineConstraints.RemoveAll(c => c != DependencyManager.CompilableDefineConstraint);
            versionDefines.Clear();
            references.Clear();
            precompiledReferences.Clear();
        }

        [Serializable]
        public class VersionDefine {

            public string name;
            public string expression;
            public string define;

            private VersionDefine(string name, string expression, string define) {
                this.name       = name;
                this.expression = expression;
                this.define     = define;
            }

            public static VersionDefine Located(string define)                                  => new("Unity", string.Empty, define);
            public static VersionDefine Invalid(string define, string reference, string status) => new("Unity", $"{status} {reference}", define);

            public override string ToString() => $"[ {define} | {name} | {expression} ]";

        }

    }

}