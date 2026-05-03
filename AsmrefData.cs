using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DependencyManagement {

    [Serializable]
    public class AsmrefData {

        public string reference;

        private string path;

        private AsmrefData(string asmrefPath, string asmdefReference) {
            reference = asmdefReference;
            path      = asmrefPath;
        }

        public AsmrefData(string asmrefPath) {
            JsonUtility.FromJsonOverwrite(File.ReadAllText(asmrefPath, Encoding.UTF8), this);
            path = asmrefPath;
        }

        public static void CreateAtPath(string asmrefPath, string asmdefReference) {
            File.WriteAllText(asmrefPath, JsonUtility.ToJson(new AsmrefData(asmrefPath, asmdefReference), true), Encoding.UTF8);
        }

        public void WriteToFile(string asmdefPath = null) {
            File.WriteAllText(asmdefPath ?? path, JsonUtility.ToJson(this, true), Encoding.UTF8);
        }

    }

}