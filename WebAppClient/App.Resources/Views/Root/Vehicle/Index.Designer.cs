namespace App.Resources.Views.Root.Vehicle {
    using System;

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Index {
        private static System.Resources.ResourceManager resourceMan;
        private static System.Globalization.CultureInfo resourceCulture;

        internal Index() {
        }

        public static System.Resources.ResourceManager ResourceManager {
            get {
                if (object.Equals(null, resourceMan)) {
                    var temp = new System.Resources.ResourceManager("WebAppClient.App.Resources.Views.Root.Vehicle.Index", typeof(Index).Assembly);
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
        public static string AddVehicle => ResourceManager.GetString("AddVehicle", resourceCulture);
        public static string NoVehicles => ResourceManager.GetString("NoVehicles", resourceCulture);
        public static string Make => ResourceManager.GetString("Make", resourceCulture);
        public static string Model => ResourceManager.GetString("Model", resourceCulture);
        public static string BatteryCapacity => ResourceManager.GetString("BatteryCapacity", resourceCulture);
        public static string CompatibleConnectors => ResourceManager.GetString("CompatibleConnectors", resourceCulture);
        public static string Edit => ResourceManager.GetString("Edit", resourceCulture);
        public static string Delete => ResourceManager.GetString("Delete", resourceCulture);
    }
}

