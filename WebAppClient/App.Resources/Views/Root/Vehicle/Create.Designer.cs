namespace App.Resources.Views.Root.Vehicle {
    using System;

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Create {
        private static System.Resources.ResourceManager resourceMan;
        private static System.Globalization.CultureInfo resourceCulture;

        internal Create() {
        }

        public static System.Resources.ResourceManager ResourceManager {
            get {
                if (object.Equals(null, resourceMan)) {
                    var temp = new System.Resources.ResourceManager("WebAppClient.App.Resources.Views.Root.Vehicle.Create", typeof(Create).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }

        public static System.Globalization.CultureInfo Culture {
            get => resourceCulture;
            set => resourceCulture = value;
        }

        public static string Title => ResourceManager.GetString("Title", resourceCulture);
        public static string Heading => ResourceManager.GetString("Heading", resourceCulture);
        public static string Make => ResourceManager.GetString("Make", resourceCulture);
        public static string Model => ResourceManager.GetString("Model", resourceCulture);
        public static string BatteryCapacity => ResourceManager.GetString("BatteryCapacity", resourceCulture);
        public static string CompatibleConnectors => ResourceManager.GetString("CompatibleConnectors", resourceCulture);
        public static string Save => ResourceManager.GetString("Save", resourceCulture);
        public static string Cancel => ResourceManager.GetString("Cancel", resourceCulture);
    }
}

